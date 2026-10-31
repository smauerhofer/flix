using Lucene.Net.Index;
using Lucene.Net.Search;

namespace Flix.Core;

// Independent retrieval paths are UNIONED, not blended into one top-N ranking.
public sealed class LuceneNameMatcher
{
  private readonly NameMatchingOptions _options;
  private readonly NameMatchEvaluator _evaluator;

  public LuceneNameMatcher() : this(new NameMatchingOptions(), new NameMatchEvaluator())
  {
  }

  public LuceneNameMatcher(NameMatchingOptions options, NameMatchEvaluator evaluator)
  {
    options.Validate();
    _options = options;
    _evaluator = evaluator;
  }

  public IReadOnlyList<NameMatch> Match(NameIndexSnapshot snapshot, string partyId,
    string name, int limit = 200, string type = "NAME")
  {
    string normalized = NameNormalizer.Normalize(name);
    if (normalized.Length == 0)
    {
      return [];
    }
    if (limit is < 1 or > 2000)
    {
      throw new ArgumentOutOfRangeException(nameof(limit));
    }
    type = FieldNormalization.CanonicalType(type);
    // The exact path is independent of fuzzy top-N limits. Fetch every matching
    // document so different sanctioned entities with the same name survive.
    Query exactQuery = ConstrainType(new TermQuery(new Term("normalizedName", normalized)), type);
    var exactHits = snapshot.Searcher.Search(exactQuery, Math.Max(1, snapshot.DocumentCount)).ScoreDocs;
    // Exact hits are ADDED to the fuzzy candidates, not a replacement for them:
    // other entities with similar spellings must still be retrieved.
    var exactDocuments = exactHits.Select(x => x.Doc).ToHashSet();
    var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
      .Distinct(StringComparer.Ordinal).ToArray();
    var paths = new List<Query>();
    var exact = new BooleanQuery();
    var fuzzy = new BooleanQuery();
    var ngrams = new BooleanQuery();
    var phonetic = new BooleanQuery();
    foreach (string token in tokens)
    {
      exact.Add(new TermQuery(new Term("nameTokens", token)), Occur.SHOULD);
      if (token.Length >= 4)
      {
        fuzzy.Add(new FuzzyQuery(new Term("nameTokens", token),
          token.Length >= 8 ? 2 : 1, 0, _options.MaxFuzzyExpansions, true), Occur.SHOULD);
      }
      string key = NamePhonetics.Key(token);
      if (key.Length > 0)
      {
        phonetic.Add(new TermQuery(new Term("namePhonetic", key)), Occur.SHOULD);
      }
    }
    foreach (string gram in NameNGrams.Generate(normalized, _options.MinNGramLength).Take(64))
    {
      ngrams.Add(new TermQuery(new Term("nameNGrams", gram)), Occur.SHOULD);
    }
    paths.Add(exact);
    // A separate all-token path survives ranking pressure from frequent individual
    // tokens and naturally supports reordered personal names.
    if (tokens.Length > 1 && tokens.Length <= 12)
    {
      var allTokens = new BooleanQuery();
      foreach (string token in tokens)
      {
        allTokens.Add(new TermQuery(new Term("nameTokens", token)), Occur.MUST);
      }
      paths.Add(allTokens);
    }
    if (fuzzy.Clauses.Count > 0)
    {
      paths.Add(fuzzy);
    }
    if (ngrams.Clauses.Count > 0)
    {
      paths.Add(ngrams);
    }
    if (phonetic.Clauses.Count > 0)
    {
      paths.Add(phonetic);
    }

    var documents = new Dictionary<int, float>();
    foreach (Query path in paths)
    {
      Query query = ConstrainType(path, type);
      foreach (ScoreDoc hit in snapshot.Searcher.Search(query, _options.CandidateLimitPerPath).ScoreDocs)
      {
        if (exactDocuments.Contains(hit.Doc))
        {
          continue;
        }
        if (!documents.TryGetValue(hit.Doc, out float previous) || hit.Score > previous)
        {
          documents[hit.Doc] = hit.Score;
        }
      }
    }
    // With default options the union cannot exceed FinalCandidateLimit (see
    // NameMatchingOptions). Exact hits are not counted: they are always returned in full.
    if (documents.Count > _options.FinalCandidateLimit)
    {
      if (exactHits.Length == 0)
      {
        // No exact hit: a truncated fuzzy search must not be presented as complete.
        throw new ScreeningLimitException("Name candidate capacity exceeded; manual review required.");
      }
      // An exact hit already makes this field a review-level hit (normal list,
      // blacklist) or is the only kind of whitelist evidence, so truncating the
      // fuzzy candidates cannot change the decision: keep the best by Lucene score.
      documents = documents
        .OrderByDescending(x => x.Value)
        .ThenBy(x => x.Key)
        .Take(_options.FinalCandidateLimit)
        .ToDictionary(x => x.Key, x => x.Value);
    }
    var exactMatches = exactHits.Select(hit => ToMatch(snapshot, partyId, name, type, hit.Doc, hit.Score));
    var fuzzyMatches = documents
      .Select(entry => ToMatch(snapshot, partyId, name, type, entry.Key, entry.Value))
      .OrderByDescending(x => x.Similarity)
      .Take(limit);
    return exactMatches.Concat(fuzzyMatches)
      .OrderByDescending(x => x.Similarity)
      .ToArray();
  }

  private NameMatch ToMatch(NameIndexSnapshot snapshot, string partyId, string name,
    string type, int document, float luceneScore)
  {
    var doc = snapshot.Searcher.Doc(document);
    string reference = doc.Get("originalName");
    return new NameMatch(partyId, doc.Get("entityId"), doc.Get("sourceId"),
      reference, _evaluator.Score(name, reference, type), luceneScore);
  }

  private static Query ConstrainType(Query path, string type)
  {
    if (type is not ("PERSON_NAME" or "ORGANIZATION_NAME"))
    {
      return path;
    }
    var allowed = new BooleanQuery();
    allowed.Add(new TermQuery(new Term("nameType", type)), Occur.SHOULD);
    allowed.Add(new TermQuery(new Term("nameType", "NAME")), Occur.SHOULD);
    var constrained = new BooleanQuery();
    constrained.Add(allowed, Occur.MUST);
    constrained.Add(path, Occur.MUST);
    return constrained;
  }

}
