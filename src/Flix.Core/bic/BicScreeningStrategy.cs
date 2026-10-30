namespace Flix.Core;

public sealed class BicScreeningStrategy(LuceneFieldMatcher matcher) : ExactFieldScreeningStrategy(matcher)
{
  public override string FieldType => "BIC";
}
