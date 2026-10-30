using Lucene.Net.Index;
using Lucene.Net.Search;

namespace Flix.Core;

// These are reference-data hits, not determinations of legal sanctions restrictions.
public sealed class LuceneFieldMatcher
{
  public IReadOnlyList<FieldMatch> Match(NameIndexSnapshot snapshot, ScreeningField field,
    int maxCandidates = 200)
  {
    string type = FieldNormalization.CanonicalType(field.Type);
    if (type is not ("IBAN" or "ACCOUNT" or "BIC" or "COUNTRY" or "ADDRESS" or "LEI"))
    {
      return Array.Empty<FieldMatch>();
    }
    // Do not throw for invalid reference identifiers: the decision service
    // records a structured data-quality finding and continues other fields.
    // This guard also protects callers using this matcher directly.
    if ((type == "LEI" && !LeiValidator.IsValid(field.Value)) ||
        (type == "IBAN" && !IbanValidator.IsValid(field.Value)))
    {
      return Array.Empty<FieldMatch>();
    }
    string value = FieldNormalization.Normalize(type, field.Value);
    if (value.Length == 0)
    {
      return Array.Empty<FieldMatch>();
    }
    if (maxCandidates < 1 || maxCandidates > 2000)
    {
      throw new ArgumentOutOfRangeException(nameof(maxCandidates));
    }

    var query = new BooleanQuery();
    query.Add(new TermQuery(new Term("fieldType", type)), Occur.MUST);
    if (type == "ADDRESS")
    {
      var alternatives = new BooleanQuery();
      alternatives.Add(new TermQuery(new Term("normalizedValue", value)), Occur.SHOULD);
      foreach (string token in value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct())
      {
        if (token.Length >= 3)
        {
          alternatives.Add(new TermQuery(new Term("addressTokens", token)), Occur.SHOULD);
        }
      }
      // Existing snapshots contain legacy addressTokens. Keep querying those terms;
      // translated abbreviations and token order are handled by the scorer.
      foreach (string token in AddressSimilarity.Tokens(field.Value).Distinct(StringComparer.Ordinal))
      {
        if (token.Length >= 3)
        {
          alternatives.Add(new TermQuery(new Term("addressTokens", token)), Occur.SHOULD);
        }
      }
      query.Add(alternatives, Occur.MUST);
    }
    else
    {
      query.Add(new TermQuery(new Term("normalizedValue", value)), Occur.MUST);
    }

    // Exact normalized lookup first: a perfect address is never pushed aside by
    // token-ranked candidates. Keep all exact matching records.
    if (type == "ADDRESS")
    {
      var exact = new BooleanQuery();
      exact.Add(new TermQuery(new Term("fieldType", type)), Occur.MUST);
      exact.Add(new TermQuery(new Term("normalizedValue", value)), Occur.MUST);
      var exactHits = snapshot.Searcher.Search(exact, Math.Max(1, snapshot.DocumentCount)).ScoreDocs;
      if (exactHits.Length > 0)
      {
        return exactHits.Select(hit =>
        {
          var document = snapshot.Searcher.Doc(hit.Doc);
          return new FieldMatch(field.PartyId, type, document.Get("entityId"),
            document.Get("sourceId"), document.Get("originalValue"), "ADDRESS_EXACT", 1.0);
        }).ToArray();
      }
    }
    var hits = snapshot.Searcher.Search(query, maxCandidates).ScoreDocs;
    var output = new List<FieldMatch>(hits.Length);
    foreach (var hit in hits)
    {
      var doc = snapshot.Searcher.Doc(hit.Doc);
      string reference = doc.Get("originalValue");
      double score = type == "ADDRESS"
        ? AddressSimilarity.Evaluate(field.Value, reference) : 1.0;
      output.Add(new FieldMatch(field.PartyId, type, doc.Get("entityId"),
        doc.Get("sourceId"), reference,
        type == "ADDRESS" ? "ADDRESS_CANDIDATE" : "EXACT", score));
    }
    return output.OrderByDescending(x => x.Score).ToArray();
  }
}
