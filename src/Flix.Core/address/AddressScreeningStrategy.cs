namespace Flix.Core;

public sealed class AddressScreeningStrategy(LuceneFieldMatcher matcher) : ExactFieldScreeningStrategy(matcher)
{
  public override string FieldType => "ADDRESS";
}
