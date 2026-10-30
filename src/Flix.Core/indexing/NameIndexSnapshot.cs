using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;

namespace Flix.Core;

public sealed class NameIndexSnapshot : IDisposable
{
  private readonly FSDirectory _directory;
  private readonly DirectoryReader _reader;
  public string Version { get; }
  public IndexSearcher Searcher { get; }
  public int DocumentCount => _reader.NumDocs;
  private int _disposed;

  public NameIndexSnapshot(string version, string indexPath)
  {
    Version = version;
    _directory = FSDirectory.Open(new DirectoryInfo(indexPath));
    try
    {
      _reader = DirectoryReader.Open(_directory);
      Searcher = new IndexSearcher(_reader);
    }
    catch
    {
      _directory.Dispose();
      throw;
    }
  }

  public void Dispose()
  {
    if (Interlocked.Exchange(ref _disposed, 1) != 0)
    {
      return;
    }
    _reader.Dispose();
    _directory.Dispose();
  }
}
