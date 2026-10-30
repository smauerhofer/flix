using System.Text.Json;
using Flix.Core;

namespace Flix.Optimizer;

// One JSON object per candidate; all candidates with the same QueryId form a ranking group.
public static class AiRankingTrainingDataFile
{
  public static IReadOnlyList<AiRankingTrainingCase> Read(string path,
    OnnxPairReranker model)
  {
    ArgumentNullException.ThrowIfNull(model);
    var rows = new List<AiRankingTrainingCase>();
    int lineNumber = 0;
    foreach (string line in File.ReadLines(path))
    {
      lineNumber++;
      if (string.IsNullOrWhiteSpace(line))
      {
        continue;
      }
      AiRankingTrainingRow row;
      try
      {
        row = JsonSerializer.Deserialize<AiRankingTrainingRow>(line,
          new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
          ?? throw new InvalidDataException("Null ranking candidate");
      }
      catch (JsonException ex)
      {
        throw new InvalidDataException($"Invalid ranking JSON at {path}:{lineNumber}", ex);
      }
      if (string.IsNullOrWhiteSpace(row.QueryId) ||
        string.IsNullOrWhiteSpace(row.Incoming) ||
        string.IsNullOrWhiteSpace(row.Reference) ||
        !double.IsFinite(row.TraditionalScore) ||
        row.TraditionalScore < 0 || row.TraditionalScore > 1)
      {
        throw new InvalidDataException($"Invalid ranking candidate at {path}:{lineNumber}");
      }
      rows.Add(new AiRankingTrainingCase(row.QueryId, row.TraditionalScore,
        model.Score(row.Incoming, row.Reference, row.TraditionalScore), row.Relevant));
    }
    return rows;
  }
}
