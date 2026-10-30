using System.Text.Json;
using Flix.Core;
namespace Flix.Optimizer;

// Each JSONL row is a complete ScreeningRequest (MessageId and Fields).
public static class TrainingDataFile
{
  public static IReadOnlyList<ScreeningRequest> Read(string path)
  {
    if (!File.Exists(path))
    {
      throw new FileNotFoundException("Training data not found", path);
    }
    var results = new List<ScreeningRequest>();
    int lineNumber = 0;
    foreach (string line in File.ReadLines(path))
    {
      lineNumber++;
      if (string.IsNullOrWhiteSpace(line))
      {
        continue;
      }
      ScreeningRequest request;
      try
      {
        request = JsonSerializer.Deserialize<ScreeningRequest>(line,
          new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
          ?? throw new InvalidDataException("Null request");
      }
      catch (JsonException ex)
      {
        throw new InvalidDataException($"Invalid JSON at {path}:{lineNumber}", ex);
      }
      if (string.IsNullOrWhiteSpace(request.MessageId) || request.Fields is null ||
        request.Fields.Count == 0)
      {
        throw new InvalidDataException($"Invalid screening request at {path}:{lineNumber}");
      }
      results.Add(request);
    }
    if (results.Count == 0)
    {
      throw new InvalidDataException($"No training cases in {path}");
    }
    return results;
  }
}
