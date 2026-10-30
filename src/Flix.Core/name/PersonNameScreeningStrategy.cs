namespace Flix.Core;

public sealed class PersonNameScreeningStrategy(LuceneNameMatcher matcher) : NameScreeningStrategy(matcher)
{
  public override string FieldType => "PERSON_NAME";
}
