namespace Flix.Core;

public sealed class ScreeningStrategyRegistry
{
  private readonly IReadOnlyDictionary<string, IScreeningStrategy> _strategies;

  public ScreeningStrategyRegistry(IEnumerable<IScreeningStrategy> strategies)
  {
    _strategies = strategies.ToDictionary(x => x.FieldType, StringComparer.Ordinal);
    string[] required = ["IBAN", "ACCOUNT", "BIC", "COUNTRY", "ADDRESS",
      "PERSON_NAME", "ORGANIZATION_NAME", "LEI"];
    if (required.Any(type => !_strategies.ContainsKey(type)))
    {
      throw new InvalidOperationException("Screening strategy registration is incomplete.");
    }
  }

  public StrategyMatches Match(NameIndexSnapshot index, ScreeningField field)
  {
    string type = FieldNormalization.CanonicalType(field.Type);
    // Legacy NAME screens against both typed name groups; deduplicate by identity.
    if (type == "NAME")
    {
      var person = _strategies["PERSON_NAME"].Match(index, field).Names;
      var org = _strategies["ORGANIZATION_NAME"].Match(index, field).Names;
      return new StrategyMatches(person.Concat(org)
        .GroupBy(x => (x.PartyId, x.EntityId, x.SourceId, x.ReferenceName))
        .Select(g => g.OrderByDescending(x => x.Similarity).First()).ToArray(), []);
    }
    if (!_strategies.TryGetValue(type, out var strategy))
    {
      throw new ArgumentException($"Unsupported screening field type: {field.Type}");
    }
    return strategy.Match(index, field);
  }
}
