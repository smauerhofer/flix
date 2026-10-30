using Lucene.Net.Index;

namespace Flix.Core;

// Built once from the immutable whitelist index when the snapshot loads.
public sealed class WhitelistGroupIndex
{
  public IReadOnlyList<WhitelistReferenceGroup> Groups { get; }

  public WhitelistGroupIndex(NameIndexSnapshot snapshot)
  {
    var groups = new Dictionary<(string Entity, string Source),
      HashSet<WhitelistReferenceCriterion>>();

    IndexReader reader = snapshot.Searcher.IndexReader;
    for (int i = 0; i < reader.MaxDoc; i++)
    {
      var document = reader.Document(i);
      string? entity = document.Get("entityId");
      string? source = document.Get("sourceId");
      string? type = document.Get("fieldType") ?? document.Get("nameType");
      string? value = document.Get("originalValue") ?? document.Get("originalName");

      if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(source) ||
        string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(value))
      {
        throw new InvalidDataException("Incomplete whitelist reference entry.");
      }

      type = FieldNormalization.CanonicalType(type);
      string normalized = FieldNormalization.Normalize(type, value);
      var key = (entity, source);
      if (!groups.TryGetValue(key, out var criteria))
      {
        criteria = new HashSet<WhitelistReferenceCriterion>();
        groups.Add(key, criteria);
      }
      criteria.Add(new WhitelistReferenceCriterion(type, normalized));
    }

    Groups = groups.Select(x => new WhitelistReferenceGroup(
      x.Key.Entity, x.Key.Source, x.Value.ToArray())).ToArray();
  }
}
