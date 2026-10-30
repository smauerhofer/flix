namespace Flix.Core;

public abstract class ExactFieldScreeningStrategy(LuceneFieldMatcher matcher) : IScreeningStrategy
{
  public abstract string FieldType { get; }
  public StrategyMatches Match(NameIndexSnapshot index, ScreeningField field)
  {
    if (FieldNormalization.CanonicalType(field.Type) != FieldType)
    {
      throw new ArgumentException($"Expected {FieldType} field.");
    }
    return new StrategyMatches([], matcher.Match(index, field));
  }
}
