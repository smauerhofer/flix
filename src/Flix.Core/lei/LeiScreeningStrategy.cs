namespace Flix.Core;

public sealed class LeiScreeningStrategy(LuceneFieldMatcher matcher) : ExactFieldScreeningStrategy(matcher)
{
  public override string FieldType => "LEI";
}
