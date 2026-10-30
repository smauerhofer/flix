using Flix.Core;

namespace Flix.Optimizer;

public sealed record ParameterChromosome
{
  public IReadOnlyList<double> Values { get; }
  public string Key { get; }

  public ParameterChromosome(IEnumerable<double> values)
  {
    Values = values.ToArray();
    if (Values.Count != ThresholdProperties.All.Count)
    {
      throw new ArgumentException("Incorrect number of threshold values.");
    }
    Key = string.Join("|", Values.Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
  }

  public FilterParameters Apply(FilterParameters basis)
  {
    // With-expression retains non-threshold properties. Expand mapping when
    // new *Threshold properties are added to FilterParameters.
    var parameters = basis with
    {
      NameReviewThreshold = Get(nameof(FilterParameters.NameReviewThreshold)),
      AddressReviewThreshold = Get(nameof(FilterParameters.AddressReviewThreshold)),
      ListNameThreshold = Get(nameof(FilterParameters.ListNameThreshold)),
      ListAddressThreshold = Get(nameof(FilterParameters.ListAddressThreshold))
    };
    parameters.Validate();
    return parameters;
  }

  public double Get(string name)
  {
    int index = -1;
    for (int i = 0; i < ThresholdProperties.All.Count; i++)
    {
      if (ThresholdProperties.All[i].Name == name)
      {
        index = i;
        break;
      }
    }
    if (index < 0)
    {
      throw new InvalidOperationException($"Missing threshold property {name}.");
    }
    return Values[index];
  }

  public static ParameterChromosome From(FilterParameters parameters)
  {
    return new ParameterChromosome(ThresholdProperties.All.Select(p => (double)p.GetValue(parameters)!));
  }
}
