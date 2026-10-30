namespace Flix.Core;

public abstract class NameScreeningStrategy(LuceneNameMatcher matcher) : IScreeningStrategy
{
  public abstract string FieldType { get; }
  public StrategyMatches Match(NameIndexSnapshot index, ScreeningField field)
  {
    string type = FieldNormalization.CanonicalType(field.Type);
    if (type != FieldType && type != "NAME")
    {
      throw new ArgumentException($"Expected {FieldType} field.");
    }
    return new StrategyMatches(matcher.Match(index, field.PartyId, field.Value,
      type: FieldType), []);
  }
}
