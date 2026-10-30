namespace Flix.Core;

public sealed class DecisionService
{
  private readonly FilterManager manager;
  private readonly ScreeningStrategyRegistry strategies;
  private readonly DecisionPolicy? legacyPolicy;
  private readonly ListOverridePolicy? legacyOverrides;

  public DecisionService(FilterManager manager, ScreeningStrategyRegistry strategies)
  {
    this.manager = manager;
    this.strategies = strategies;
  }

  // Compatibility with callers using the previous API.
  public DecisionService(FilterManager manager, ScreeningStrategyRegistry strategies,
    DecisionPolicy policy, ListOverridePolicy overrides)
  {
    this.manager = manager;
    this.strategies = strategies;
    legacyPolicy = policy;
    legacyOverrides = overrides;
  }

  public ScreeningDecision Screen(ScreeningRequest request)
  {
    return ScreenWithPolicies(request, null, null);
  }

  // For offline calibration only: same live matching logic, explicit candidate policies.
  public ScreeningDecision ScreenWithPolicies(ScreeningRequest request,
    DecisionPolicy? policyOverride, ListOverridePolicy? overridePolicy)
  {
    ArgumentNullException.ThrowIfNull(request);
    using var lease = manager.Acquire();
    DecisionPolicy policy = policyOverride ?? legacyPolicy ?? lease.Filter.DecisionPolicy;
    ListOverridePolicy overrides = overridePolicy ?? legacyOverrides ?? lease.Filter.OverridePolicy;
    if (string.IsNullOrWhiteSpace(request.MessageId) || request.Fields is null ||
      request.Fields.Count == 0 || request.Fields.Count > policy.MaxFields)
    {
      throw new ArgumentException("Missing message ID or invalid field count.");
    }

    TenantListSnapshot? tenant = lease.Snapshot.GetTenant(request.TenantId);
    var validationFindings = new List<ScreeningValidationFinding>();
    var invalidSides = new HashSet<string>(StringComparer.Ordinal);
    var names = new List<NameMatch>();
    var fields = new List<FieldMatch>();
    var black = new List<ListFinding>();
    var white = new List<ListFinding>();
    var aiEvidence = new List<AiMatchEvidence>();
    var sideFields = new Dictionary<string, int>(StringComparer.Ordinal);
    var tenantWhiteMatchesBySide = new Dictionary<string,
      List<(ScreeningField Field, IReadOnlyList<ListFinding> Findings)>>(StringComparer.Ordinal);
    var whiteMatchesBySide = new Dictionary<string,
      List<(ScreeningField Field, IReadOnlyList<ListFinding> Findings)>>(StringComparer.Ordinal);
    var groupMatcher = new WhitelistGroupMatcher();
    var sideNormalHits = new HashSet<string>(StringComparer.Ordinal);
    var sideBlackHits = new HashSet<string>(StringComparer.Ordinal);
    var sideCountryBlocks = new HashSet<string>(StringComparer.Ordinal);

    try
    {
    foreach (var field in request.Fields)
    {
      if (field is null || string.IsNullOrWhiteSpace(field.PartyId) ||
        string.IsNullOrWhiteSpace(field.Type) || string.IsNullOrWhiteSpace(field.Value) ||
        field.Value.Length > 1024 || !FieldNormalization.Supported(field.Type))
      {
        throw new ArgumentException("Invalid screening field.");
      }
      string side = ScreeningSideResolver.Resolve(field.PartyId);
      sideFields[side] = sideFields.GetValueOrDefault(side) + 1;

      var validation = ScreeningFieldValidator.Validate(field, side);
      if (validation is not null)
      {
        validationFindings.Add(validation);
        invalidSides.Add(side);
        // An invalid field never qualifies for a whitelist. Keep its position
        // in the side so that all-criteria matching cannot silently pass.
        if (!whiteMatchesBySide.TryGetValue(side, out var invalidSideFields))
        {
          invalidSideFields = new List<(ScreeningField, IReadOnlyList<ListFinding>)>();
          whiteMatchesBySide.Add(side, invalidSideFields);
        }
        invalidSideFields.Add((field, Array.Empty<ListFinding>()));
        if (!tenantWhiteMatchesBySide.TryGetValue(side, out var tenantInvalidFields))
        {
          tenantInvalidFields = new List<(ScreeningField, IReadOnlyList<ListFinding>)>();
          tenantWhiteMatchesBySide.Add(side, tenantInvalidFields);
        }
        tenantInvalidFields.Add((field, Array.Empty<ListFinding>()));
        continue;
      }

      var normalMatches = strategies.Match(lease.Snapshot.Normal, field);
      names.AddRange(normalMatches.Names);
      fields.AddRange(normalMatches.Fields);
      RankCandidates("NORMAL", field, normalMatches, lease.Filter, aiEvidence);
      string type = FieldNormalization.CanonicalType(field.Type);
      // COUNTRY in the normal (sanctions) list is evidence only: it describes a listed
      // target, not an embargo. Country blocking is driven by blacklist COUNTRY entries.
      if (normalMatches.Fields.Any(hit => hit.FieldType switch
          {
            "ADDRESS" => hit.Score >= policy.AddressReviewThreshold,
            "IBAN" or "ACCOUNT" or "BIC" or "LEI" => true,
            _ => false
          }) || normalMatches.Names.Any(hit => hit.Similarity >= policy.NameReviewThreshold))
      {
        sideNormalHits.Add(side);
      }

      int beforeBlack = black.Count;
      Collect("GLOBAL_BLACKLIST", lease.Snapshot.Blacklist, field, black, overrides,
        lease.Filter, aiEvidence);
      if (tenant is not null)
      {
        Collect("TENANT_BLACKLIST", tenant.Blacklist, field, black, overrides,
          lease.Filter, aiEvidence);
      }
      if (black.Count > beforeBlack)
      {
        sideBlackHits.Add(side);
        if (black.Skip(beforeBlack).Any(x => x.FieldType == "COUNTRY"))
        {
          sideCountryBlocks.Add(side);
        }
      }
      int beforeWhite = white.Count;
      Collect("GLOBAL_WHITELIST", lease.Snapshot.Whitelist, field, white, overrides,
        lease.Filter, aiEvidence);
      if (!whiteMatchesBySide.TryGetValue(side, out var sideWhiteFields))
      {
        sideWhiteFields = new List<(ScreeningField, IReadOnlyList<ListFinding>)>();
        whiteMatchesBySide.Add(side, sideWhiteFields);
      }
      sideWhiteFields.Add((field, white.Skip(beforeWhite).ToArray()));
      // Keep each scope's evidence separate: whitelist groups cannot span scopes.
      if (!tenantWhiteMatchesBySide.TryGetValue(side, out var tenantWhiteFields))
      {
        tenantWhiteFields = new List<(ScreeningField, IReadOnlyList<ListFinding>)>();
        tenantWhiteMatchesBySide.Add(side, tenantWhiteFields);
      }
      int beforeTenantWhite = white.Count;
      if (tenant is not null)
      {
        Collect("TENANT_WHITELIST", tenant.Whitelist, field, white, overrides,
          lease.Filter, aiEvidence);
      }
      tenantWhiteFields.Add((field, white.Skip(beforeTenantWhite).ToArray()));
      if (names.Count + fields.Count + black.Count + white.Count > policy.MaxMatches)
      {
        throw new ScreeningLimitException("Candidate limit exceeded; manual review required.");
      }
    }

    }
    catch (ScreeningLimitException exception)
    {
      // A capacity overflow cannot be treated as CLEAR or a routine error.
      // Keep all accumulated evidence and route the entire message to review.
      var capacitySides = request.Fields
        .GroupBy(x => ScreeningSideResolver.Resolve(x.PartyId), StringComparer.Ordinal)
        .Select(group => new SideScreeningDecision(group.Key, group.Count(), 0,
          true, false, false, false, "REVIEW"))
        .OrderBy(x => x.Side, StringComparer.Ordinal).ToArray();
      return new ScreeningDecision(request.MessageId, lease.Snapshot.Version,
        "REVIEW", false, "Screening capacity exceeded; manual review required: " + exception.Message,
        [], names.ToArray(), fields.ToArray(), DateTimeOffset.UtcNow,
        black.ToArray(), white.ToArray(), false, capacitySides,
        validationFindings.ToArray(), request.TenantId, aiEvidence.ToArray());
    }

    var entities = names.Select(x => (x.PartyId, x.EntityId))
      .Concat(fields.Select(x => (x.PartyId, x.EntityId)))
      .Distinct()
      .Select(key =>
      {
        var nm = names.Where(x => x.PartyId == key.PartyId && x.EntityId == key.EntityId).ToArray();
        var fm = fields.Where(x => x.PartyId == key.PartyId && x.EntityId == key.EntityId).ToArray();
        return new EntityEvidence(key.PartyId, key.EntityId,
          nm.Select(x => x.SourceId).Concat(fm.Select(x => x.SourceId)).Distinct().ToArray(),
          nm.Length == 0 ? 0 : nm.Max(x => x.Similarity),
          fm.Any(x => x.FieldType is "IBAN" or "ACCOUNT" or "BIC" or "LEI"),
          fm.Any(x => x.FieldType == "ADDRESS" && x.Score >= policy.AddressReviewThreshold),
          fm.Any(x => x.FieldType == "COUNTRY"));
      }).ToArray();

    var sideDecisions = sideFields.Select(entry =>
    {
      string side = entry.Key;
      bool normalHit = sideNormalHits.Contains(side);
      bool blackHit = sideBlackHits.Contains(side);
      var sideWhiteFields = whiteMatchesBySide[side];
      bool globalWhiteHit = !invalidSides.Contains(side) && groupMatcher.Matches(
        sideWhiteFields.Select(x => x.Field).ToArray(),
        sideWhiteFields.Select(x => x.Findings).ToArray(),
        lease.Snapshot.WhitelistGroups.Groups);
      var tenantWhiteFields = tenantWhiteMatchesBySide[side];
      bool tenantWhiteHit = tenant is not null && !invalidSides.Contains(side) && groupMatcher.Matches(
        tenantWhiteFields.Select(x => x.Field).ToArray(),
        tenantWhiteFields.Select(x => x.Findings).ToArray(),
        tenant.WhitelistGroups.Groups);
      bool whiteHit = globalWhiteHit || tenantWhiteHit;
      int matchedFieldCount = sideWhiteFields.Zip(tenantWhiteFields,
        (global, local) => global.Findings.Count != 0 || local.Findings.Count != 0).Count(x => x);
      // A blocked country always wins, regardless of WhitelistOverridesAll or precedence.
      bool countryBlock = sideCountryBlocks.Contains(side);
      bool overrideApplied = whiteHit && !countryBlock &&
        (overrides.WhitelistOverridesAll ||
         overrides.Precedence == ListPrecedence.WhitelistFirst || !blackHit);
      bool review = invalidSides.Contains(side) || countryBlock ||
        (!overrideApplied && (blackHit || normalHit));
      string decision = overrideApplied ? "WHITELISTED" : review ? "REVIEW" : "CLEAR";
      return new SideScreeningDecision(side, entry.Value,
        matchedFieldCount, normalHit, blackHit,
        whiteHit, overrideApplied, decision, countryBlock);
    }).OrderBy(x => x.Side, StringComparer.Ordinal).ToArray();

    // Each side is evaluated independently. The message hits if EITHER side
    // remains in REVIEW; a whitelist on one side never clears the other side.
    bool reviewMessage = sideDecisions.Any(x => x.Decision == "REVIEW");
    bool anyOverride = sideDecisions.Any(x => x.WhitelistOverrideApplied);
    bool allWhitelisted = sideDecisions.All(x => x.Decision == "WHITELISTED");
    string messageDecision = reviewMessage ? "REVIEW" :
      allWhitelisted ? "WHITELISTED" : "CLEAR";
    string reason = validationFindings.Count != 0
      ? "Invalid screening identifier(s); manual review required" :
      sideCountryBlocks.Count != 0
      ? "Blocked country on at least one side; whitelist override not permitted" :
      reviewMessage ? "At least one side requires review" :
      anyOverride ? "Qualified side whitelist(s) applied; other side independently evaluated" :
      "No review-level hit on either side";
    return new ScreeningDecision(request.MessageId, lease.Snapshot.Version,
      messageDecision, !reviewMessage, reason, entities, names, fields,
      DateTimeOffset.UtcNow, black, white, anyOverride, sideDecisions,
      validationFindings, request.TenantId,
      aiEvidence.OrderBy(x => x.ListScope, StringComparer.Ordinal)
        .ThenBy(x => x.PartyId, StringComparer.Ordinal)
        .ThenBy(x => x.FieldType, StringComparer.Ordinal)
        .ThenBy(x => x.Rank).ToArray());
  }

  private void Collect(string list, NameIndexSnapshot snapshot, ScreeningField field,
    List<ListFinding> output, ListOverridePolicy overrides,
    ScreeningFilter filter, List<AiMatchEvidence> aiEvidence)
  {
    string type = FieldNormalization.CanonicalType(field.Type);
    var matches = strategies.Match(snapshot, field);
    RankCandidates(list, field, matches, filter, aiEvidence);
    // A whitelist releases payments, so it only qualifies on the exact normalized
    // value. Fuzzy similarity (and token re-ordering, which also scores 1.0) is
    // evidence for review, never for release. Blacklists keep fuzzy thresholds.
    bool exactOnly = list is "GLOBAL_WHITELIST" or "TENANT_WHITELIST";
    string incoming = FieldNormalization.Normalize(type, field.Value);
    foreach (var hit in matches.Names)
    {
      bool qualifies = exactOnly
        ? FieldNormalization.Normalize(type, hit.ReferenceName) == incoming
        : hit.Similarity >= overrides.NameThreshold;
      if (qualifies)
      {
        output.Add(new ListFinding(list, hit.PartyId, hit.EntityId,
          hit.SourceId, type, hit.ReferenceName, hit.Similarity));
      }
    }
    foreach (var hit in matches.Fields)
    {
      bool qualifies = exactOnly
        ? FieldNormalization.Normalize(type, hit.ReferenceValue) == incoming
        : type != "ADDRESS" || hit.Score >= overrides.AddressThreshold;
      if (qualifies)
      {
        output.Add(new ListFinding(list, hit.PartyId, hit.EntityId,
          hit.SourceId, type, hit.ReferenceValue, hit.Score));
      }
    }
  }

  // AI reranks retrieved candidates for review presentation only. It does not
  // affect blacklist/whitelist membership, side decisions, or release policy.
  private static void RankCandidates(string scope, ScreeningField field,
    StrategyMatches matches, ScreeningFilter filter, List<AiMatchEvidence> output)
  {
    OnnxPairReranker? reranker = filter.AiReranker;
    if (reranker is null)
    {
      return;
    }
    string type = FieldNormalization.CanonicalType(field.Type);
    if (type is not ("NAME" or "PERSON_NAME" or "ORGANIZATION_NAME" or "ADDRESS"))
    {
      return;
    }
    var candidates = matches.Names.Select(x => (
      x.EntityId, x.SourceId, Reference: x.ReferenceName, Score: x.Similarity))
      .Concat(matches.Fields.Where(x => x.FieldType == "ADDRESS")
        .Select(x => (x.EntityId, x.SourceId, Reference: x.ReferenceValue, Score: x.Score)))
      .OrderByDescending(x => x.Score)
      .Take(filter.Parameters.AI.MaxCandidatesPerField)
      .Select(x => new
      {
        x.EntityId,
        x.SourceId,
        x.Reference,
        x.Score,
        AiScore = reranker.Score(field.Value, x.Reference, x.Score)
      })
      .Select(x => new
      {
        x.EntityId, x.SourceId, x.Reference, x.Score, x.AiScore,
        CombinedScore = AiRankingPolicy.Combine(x.Score, x.AiScore, filter.Parameters.AI)
      })
      .OrderByDescending(x => x.CombinedScore)
      .ThenByDescending(x => x.Score)
      .ToArray();
    for (int i = 0; i < candidates.Length; i++)
    {
      var candidate = candidates[i];
      output.Add(new AiMatchEvidence(scope, field.PartyId, type,
        candidate.EntityId, candidate.SourceId, candidate.Reference,
        candidate.Score, candidate.AiScore, reranker.ModelVersion, i + 1,
        candidate.CombinedScore));
    }
  }

}
