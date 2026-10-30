namespace Flix.Optimizer;

public sealed record AiRankingTrainingRow(
  string QueryId, string Incoming, string Reference,
  double TraditionalScore, bool Relevant);
