using System.Text.Json;
using Flix.Core;

namespace Flix.Api;

// Local durable journal. For clustered deployments replace with a transactional
// workflow/outbox audit sink. A failed write MUST prevent a CLEAR release response.
public sealed class AuditJournal : IDisposable
{
  public string Root { get; }
  private readonly object _gate = new();
  private readonly FileStream _stream;
  public AuditJournal(string root)
  {
    Root = root;
    var path = Path.Combine(root, "audit");
    Directory.CreateDirectory(path);
    _stream = new FileStream(Path.Combine(path, "decisions.jsonl"),
      FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
  }
  public void Append(ScreeningDecision decision)
  {
    byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(decision);
    lock (_gate)
    {
      _stream.Write(bytes);
      _stream.WriteByte((byte)'\n');
      _stream.Flush(flushToDisk: true);
    }
  }
  public void Dispose() => _stream.Dispose();
}
