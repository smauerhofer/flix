using System.Text.Json;

namespace Flix.Core;

// Single-writer append journal for review hit cases (hits.jsonl), shared by all
// concurrent screening requests of the API process. Like AuditJournal it keeps one
// stream open and serializes writes with a lock, so parallel REVIEW results no
// longer collide on opening the file. Each record is written as one complete line
// and flushed to disk before the screening response is returned.
//
// Readers (Hit Reviewer) must open the file with FileShare.ReadWrite while the API
// runs; HitCaseFile.Read does so and ignores a trailing line still being written.
public sealed class HitCaseJournal : IDisposable
{
  private readonly object _gate = new();
  private readonly FileStream _stream;

  public string Path { get; }

  public HitCaseJournal(string path)
  {
    Path = System.IO.Path.GetFullPath(path);
    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
    _stream = new FileStream(Path, FileMode.Append, FileAccess.Write,
      FileShare.Read, 4096, FileOptions.WriteThrough);
  }

  public void Append(HitCase hit)
  {
    HitCaseFile.Validate(hit);
    byte[] json = JsonSerializer.SerializeToUtf8Bytes(hit);
    byte[] line = new byte[json.Length + 1];
    json.CopyTo(line, 0);
    line[^1] = (byte)'\n';
    lock (_gate)
    {
      _stream.Write(line);
      _stream.Flush(flushToDisk: true);
    }
  }

  public void Dispose()
  {
    lock (_gate)
    {
      _stream.Dispose();
    }
  }
}
