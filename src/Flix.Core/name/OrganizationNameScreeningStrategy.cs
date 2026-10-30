namespace Flix.Core;

public sealed class OrganizationNameScreeningStrategy(LuceneNameMatcher matcher) : NameScreeningStrategy(matcher)
{
  public override string FieldType => "ORGANIZATION_NAME";
}
