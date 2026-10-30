namespace Flix.Core;

public sealed class AccountNumberScreeningStrategy(LuceneFieldMatcher matcher) : ExactFieldScreeningStrategy(matcher)
{
  public override string FieldType => "ACCOUNT";
}
