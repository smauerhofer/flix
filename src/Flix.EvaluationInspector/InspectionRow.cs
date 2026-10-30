using Flix.Core;

namespace Flix.EvaluationInspector;

public sealed class InspectionRow
{
  public required PositiveEvaluationResult Result { get; init; }
  public PositiveEvaluationCase? Case { get; init; }
  public string CaseId => Result.CaseId;
  public string Decision => Result.Decision;
  public string Tenant => Result.TenantId;
  public string Strategy => Result.Strategy;
  public string Generation => Result.GenerationType;
  public string Diagnostic => Result.Diagnostic ?? Result.Error ?? string.Empty;
  public bool Retrieved => Result.CandidateRetrieved ?? Result.ExpectedEntityFound;
  public bool ExpectedFound => Result.ExpectedEntityFound;
  public double? ExpectedScore => Result.ExpectedEntityScore;
  public double? Threshold => Result.ReviewThreshold;
  public double Milliseconds => Result.ElapsedMilliseconds;
  public string Incoming => Case?.Field?.Value ?? string.Empty;
  public string Mutation => Case is null ? string.Empty : Case.GenerationType;
  public string Reference => $"{Result.ExpectedSourceId} / {Result.ExpectedEntityId}";
}
