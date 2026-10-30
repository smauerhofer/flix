using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Flix.Core;

// Expects a locally supplied, independently validated binary classifier ONNX
// with input "features" float32[1,6] and output "score" float32[1,1].
public sealed class OnnxPairReranker : IDisposable
{
  private readonly InferenceSession session;
  public string ModelVersion { get; }

  public OnnxPairReranker(AiMatchingOptions options)
  {
    options.Validate();
    if (!options.Enabled)
    {
      throw new ArgumentException("AI reranker is not enabled.");
    }
    if (!File.Exists(options.ModelPath))
    {
      throw new FileNotFoundException("Configured local AI model not found.", options.ModelPath);
    }
    ModelVersion = options.ModelVersion;
    session = new InferenceSession(options.ModelPath);
    if (!session.InputMetadata.TryGetValue("features", out var input) ||
      input.ElementType != typeof(float) ||
      !session.OutputMetadata.TryGetValue("score", out var output) ||
      output.ElementType != typeof(float))
    {
      session.Dispose();
      throw new InvalidDataException("AI model must expose float input 'features' and output 'score'.");
    }
  }

  public double Score(string incoming, string reference, double traditionalScore)
  {
    float[] features = AiPairFeatures.Extract(incoming, reference, traditionalScore);
    var tensor = new DenseTensor<float>(features, [1, AiPairFeatures.Dimension]);
    using var results = session.Run([
      NamedOnnxValue.CreateFromTensor("features", tensor)
    ]);
    float value = results.Single(x => x.Name == "score").AsTensor<float>().ToArray().Single();
    if (!float.IsFinite(value) || value < 0 || value > 1)
    {
      throw new InvalidDataException("AI model produced an invalid probability.");
    }
    return value;
  }

  public void Dispose()
  {
    session.Dispose();
  }
}
