namespace Flix.Core;

public interface IScreeningStrategy
{
  string FieldType { get; }
  StrategyMatches Match(NameIndexSnapshot index, ScreeningField field);
}
