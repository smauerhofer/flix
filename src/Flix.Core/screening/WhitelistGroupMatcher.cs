namespace Flix.Core;

// Qualifies one side only when one complete whitelist entry matches it.
public sealed class WhitelistGroupMatcher
{
  public bool Matches(
    IReadOnlyList<ScreeningField> sideFields,
    IReadOnlyList<IReadOnlyList<ListFinding>> findingsByField,
    IReadOnlyList<WhitelistReferenceGroup> groups)
  {
    if (sideFields.Count == 0 || findingsByField.Count != sideFields.Count)
    {
      return false;
    }

    foreach (var group in groups)
    {
      // Every input criterion must match this specific whitelist entry.
      bool allInputsMatched = true;
      var matchedCriteria = new HashSet<WhitelistReferenceCriterion>();
      for (int i = 0; i < sideFields.Count; i++)
      {
        bool fieldMatched = false;
        foreach (var finding in findingsByField[i])
        {
          if (finding.EntityId != group.EntityId || finding.SourceId != group.SourceId)
          {
            continue;
          }
          var criterion = new WhitelistReferenceCriterion(
            FieldNormalization.CanonicalType(finding.FieldType),
            FieldNormalization.Normalize(finding.FieldType, finding.ReferenceValue));
          if (group.Criteria.Contains(criterion))
          {
            fieldMatched = true;
            matchedCriteria.Add(criterion);
          }
        }
        if (!fieldMatched)
        {
          allInputsMatched = false;
          break;
        }
      }

      // Every element stored in the whitelist group must also have matched.
      // Extra elements in a reference entry cannot be silently ignored.
      if (allInputsMatched && group.Criteria.All(matchedCriteria.Contains))
      {
        return true;
      }
    }
    return false;
  }
}
