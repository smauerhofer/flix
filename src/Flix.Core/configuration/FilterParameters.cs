using YamlDotNet.Serialization;

namespace Flix.Core;

// Immutable settings captured together with a screening snapshot.
public sealed record FilterParameters
{
  public AiMatchingOptions AI { get; init; } = new();
  public string Root { get; init; } = "../../flix-data";
  public double NameReviewThreshold { get; init; } = 0.82;
  public double AddressReviewThreshold { get; init; } = 0.90;
  // Safe defaults: a whitelist never overrides a blacklist hit unless explicitly configured.
  public bool WhitelistOverridesAll { get; init; } = false;
  public ListPrecedence ListPrecedence { get; init; } = ListPrecedence.BlacklistFirst;
  public double ListNameThreshold { get; init; } = 0.92;
  public double ListAddressThreshold { get; init; } = 0.95;

  public void Validate()
  {
    AI.Validate();
    if (string.IsNullOrWhiteSpace(Root))
    {
      throw new InvalidDataException("Root is required.");
    }
    if (!Enum.IsDefined(ListPrecedence))
    {
      throw new InvalidDataException("Invalid list precedence.");
    }
    foreach (double threshold in new[] { NameReviewThreshold, AddressReviewThreshold,
      ListNameThreshold, ListAddressThreshold })
    {
      if (!double.IsFinite(threshold) || threshold < 0 || threshold > 1)
      {
        throw new InvalidDataException("Matching thresholds must be between 0 and 1.");
      }
    }
  }

  [YamlIgnore]
  public DecisionPolicy DecisionPolicy =>
    new(NameReviewThreshold, AddressReviewThreshold);

  [YamlIgnore]
  public ListOverridePolicy OverridePolicy =>
    new(WhitelistOverridesAll, ListPrecedence, ListNameThreshold, ListAddressThreshold);
}
