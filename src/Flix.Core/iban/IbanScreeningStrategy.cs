namespace Flix.Core;

public sealed class IbanScreeningStrategy(LuceneFieldMatcher matcher) : ExactFieldScreeningStrategy(matcher)
{
  public override string FieldType => "IBAN";
}
