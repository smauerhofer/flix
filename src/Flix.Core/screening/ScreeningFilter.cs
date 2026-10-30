namespace Flix.Core;

// The snapshot and its settings travel together throughout a screening lease.
public sealed class ScreeningFilter : IDisposable
{
  public FilterSnapshot Snapshot { get; }
  public OnnxPairReranker? AiReranker { get; }
  public FilterParameters Parameters { get; }
  public DecisionPolicy DecisionPolicy { get; }
  public ListOverridePolicy OverridePolicy { get; }

  public ScreeningFilter(FilterSnapshot snapshot, FilterParameters parameters)
  {
    Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    parameters.Validate();
    DecisionPolicy = parameters.DecisionPolicy;
    OverridePolicy = parameters.OverridePolicy;
    if (parameters.AI.Enabled)
    {
      AiReranker = new OnnxPairReranker(parameters.AI);
    }
  }
  public void Dispose()
  {
    AiReranker?.Dispose();
    Snapshot.Dispose();
  }
}
