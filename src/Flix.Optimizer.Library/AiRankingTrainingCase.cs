namespace Flix.Optimizer;

// A labeled candidate. The score has already been computed by the configured ONNX model.
public sealed record AiRankingTrainingCase(
  string QueryId, double TraditionalScore, double AiScore, bool Relevant);
