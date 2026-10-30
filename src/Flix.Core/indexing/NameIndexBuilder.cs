using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace Flix.Core;

// Build against a NEVER-PUBLISHED directory. No readers can see partial data.
public sealed class NameIndexBuilder : IDisposable
{
  private readonly FSDirectory _directory;
  // Bump whenever indexed fields or their tokenization change; snapshots must be rebuilt.
  public const string AlgorithmVersion = "hybrid-name-v3";

  private readonly Analyzer _analyzer;
  private readonly IndexWriter _writer;
  private bool _completed;

  public NameIndexBuilder(string path)
  {
    if (System.IO.Directory.Exists(path) && System.IO.Directory.EnumerateFileSystemEntries(path).Any())
    {
      throw new InvalidOperationException("New index directory must be empty.");
    }
    System.IO.Directory.CreateDirectory(path);
    _directory = FSDirectory.Open(new DirectoryInfo(path));
    // Tokenized fields must split exactly like NameNormalizer; see IndexTokenAnalyzer.
    _analyzer = IndexTokenAnalyzer.Create();
    _writer = new IndexWriter(_directory, new IndexWriterConfig(LuceneVersion.LUCENE_48, _analyzer)
    {
      OpenMode = OpenMode.CREATE
    });
  }

  public void AddName(NameRecord record) => AddTypedName(record, "NAME");

  public void AddTypedName(NameRecord record, string nameType)
  {
    nameType = FieldNormalization.CanonicalType(nameType);
    if (nameType is not ("NAME" or "PERSON_NAME" or "ORGANIZATION_NAME"))
    {
      throw new ArgumentException("Unsupported name type.");
    }
    if (_completed)
    {
      throw new InvalidOperationException("Index is already committed.");
    }
    var normalized = NameNormalizer.Normalize(record.Name);
    if (normalized.Length == 0)
    {
      return;
    }
    if (string.IsNullOrWhiteSpace(record.EntityId) || string.IsNullOrWhiteSpace(record.SourceId))
    {
      throw new ArgumentException("EntityId and SourceId are required.");
    }
    var nameDocument = new Document
    {
      new StringField("entityId", record.EntityId, Field.Store.YES),
      new StringField("sourceId", record.SourceId, Field.Store.YES),
      new StringField("nameType", nameType, Field.Store.YES),
      new StringField("originalName", record.Name, Field.Store.YES),
      new StringField("normalizedName", normalized, Field.Store.YES),
      new TextField("nameTokens", normalized, Field.Store.NO),
      new StringField("matchingAlgorithm", AlgorithmVersion, Field.Store.YES)
    };
    foreach (string gram in NameNGrams.Generate(normalized))
    {
      nameDocument.Add(new StringField("nameNGrams", gram, Field.Store.NO));
    }
    foreach (string token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    {
      string key = NamePhonetics.Key(token);
      if (key.Length > 0)
      {
        nameDocument.Add(new StringField("namePhonetic", key, Field.Store.NO));
      }
    }
    _writer.AddDocument(nameDocument);
  }

  // All field types are written to the SAME unpublished Lucene snapshot.
  public void AddField(ReferenceFieldRecord record)
  {
    if (_completed)
    {
      throw new InvalidOperationException("Index is already committed.");
    }
    string type = FieldNormalization.CanonicalType(record.Type);
    if (!FieldNormalization.Supported(type))
    {
      throw new ArgumentException($"Unsupported field type: {type}");
    }
    if (type is "NAME" or "PERSON_NAME" or "ORGANIZATION_NAME")
    {
      AddTypedName(new NameRecord(record.EntityId, record.SourceId, record.Value), type);
      return;
    }
    if (string.IsNullOrWhiteSpace(record.EntityId) || string.IsNullOrWhiteSpace(record.SourceId))
    {
      throw new ArgumentException("EntityId and SourceId are required.");
    }
    if (type == "LEI" && !LeiValidator.IsValid(record.Value))
    {
      throw new ArgumentException("Invalid reference LEI checksum or format.");
    }
    string normalized = FieldNormalization.Normalize(type, record.Value);
    if (normalized.Length == 0)
    {
      throw new ArgumentException("Empty reference field.");
    }
    var document = new Document
    {
      new StringField("entityId", record.EntityId, Field.Store.YES),
      new StringField("sourceId", record.SourceId, Field.Store.YES),
      new StringField("fieldType", type, Field.Store.YES),
      new StringField("originalValue", record.Value, Field.Store.YES),
      new StringField("normalizedValue", normalized, Field.Store.YES)
    };
    if (type == "ADDRESS")
    {
      document.Add(new TextField("addressTokens", normalized, Field.Store.NO));
    }
    _writer.AddDocument(document);
  }

  public void Complete()
  {
    if (_completed)
    {
      throw new InvalidOperationException("Already committed.");
    }
    _writer.Commit();
    _completed = true;
  }

  public void Dispose()
  {
    _writer.Dispose();
    _analyzer.Dispose();
    _directory.Dispose();
  }
}
