using System.Text.Json;

namespace Flix.Core;

public sealed class ReviewCaseProcessor
{
  private readonly ReviewFilePaths _paths;
  private readonly object _sync = new();
  private readonly HashSet<string> _processed;

  public ReviewCaseProcessor(ReviewFilePaths paths)
  {
    _paths = paths;
    _processed = LoadProcessed(paths.Processed);
  }

  public bool IsProcessed(string caseId)
  {
    lock (_sync)
    {
      return _processed.Contains(caseId);
    }
  }

  public ReviewDisposition Apply(HitCase hit, string action, string actor,
    string comment, bool addToWhitelist = false)
  {
    if (hit.Message is null || hit.Decision is null ||
        hit.Message.MessageId != hit.Decision.MessageId)
    {
      throw new InvalidDataException("Invalid hit case.");
    }
    if (action != "RELEASE" && action != "REJECT" && action != "INVESTIGATE")
    {
      throw new ArgumentException("Unknown disposition.", nameof(action));
    }
    if (string.IsNullOrWhiteSpace(actor))
    {
      throw new ArgumentException("Reviewer identity required.", nameof(actor));
    }
    if (addToWhitelist && action != "RELEASE")
    {
      throw new ArgumentException("Whitelist addition is only permitted with release.");
    }
    lock (_sync)
    {
      if (_processed.Contains(hit.CaseId))
      {
        throw new InvalidOperationException("Case already processed.");
      }
      hit.Comments ??= [];
      if (!string.IsNullOrWhiteSpace(comment))
      {
        hit.Comments.Add(new ReviewComment
        {
          Actor = actor,
          Text = comment.Trim()
        });
      }
      if (addToWhitelist)
      {
        AddWhitelistGroup(hit);
      }
      var disposition = new ReviewDisposition
      {
        CaseId = hit.CaseId,
        MessageId = hit.Message.MessageId,
        Action = action,
        Actor = actor,
        WhitelistAdded = addToWhitelist,
        Hit = hit
      };
      string output = action switch
      {
        "RELEASE" => _paths.Release,
        "REJECT" => _paths.Reject,
        _ => _paths.Investigate
      };
      JsonLineJournal.Append(output, disposition);
      JsonLineJournal.Append(_paths.Processed, new { hit.CaseId, action, disposition.AtUtc });
      _processed.Add(hit.CaseId);
      return disposition;
    }
  }

  private void AddWhitelistGroup(HitCase hit)
  {
    IReadOnlyList<ScreeningField> fields = hit.Message!.Fields;
    string[] parties = fields.Select(f => f.PartyId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    if (parties.Length != 1)
    {
      throw new InvalidOperationException(
        "Select a case containing exactly one party to add a whitelist group. Multi-party release cannot create a global whitelist.");
    }
    string side = ScreeningSideResolver.Resolve(parties[0]);
    if (string.IsNullOrWhiteSpace(side) || side == "UNKNOWN")
    {
      throw new InvalidOperationException("Unknown screening side.");
    }
    var groups = File.Exists(_paths.Whitelist)
      ? WhitelistFile.Read(_paths.Whitelist).ToList()
      : new List<WhitelistGroupEntry>();
    var group = new WhitelistGroupEntry
    {
      EntityId = "REVIEW-" + Guid.NewGuid().ToString("N"),
      SourceId = "MANUAL-REVIEW",
      Description = $"Case {hit.CaseId}; {side}; requires approval before publishing",
      Active = false,
      Criteria = fields.Select(field => new WhitelistCriterion
      {
        Type = field.Type,
        Value = field.Value,
        Active = true
      }).ToList()
    };
    groups.Add(group);
    WhitelistFile.Write(_paths.Whitelist, groups);
  }

  private static HashSet<string> LoadProcessed(string path)
  {
    var result = new HashSet<string>(StringComparer.Ordinal);
    if (!File.Exists(path))
    {
      return result;
    }
    foreach (string line in File.ReadLines(path))
    {
      if (string.IsNullOrWhiteSpace(line))
      {
        continue;
      }
      using JsonDocument doc = JsonDocument.Parse(line);
      string? id = doc.RootElement.GetProperty("CaseId").GetString();
      if (!string.IsNullOrWhiteSpace(id))
      {
        result.Add(id);
      }
    }
    return result;
  }
}
