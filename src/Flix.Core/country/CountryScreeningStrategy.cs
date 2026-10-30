namespace Flix.Core;

public sealed class CountryScreeningStrategy(LuceneFieldMatcher matcher) : ExactFieldScreeningStrategy(matcher)
{
  public override string FieldType => "COUNTRY";
}
