namespace Flix.Core;

public sealed record SecoSpellingVariant(string Value, string? Language = null);

// IsOptional: a part that is not part of the name itself (SECO name-part-type "title",
// e.g. "General" or "Manager of ..."). It is kept in the published name only.
public sealed record SecoNamePart(int Order, string? Value, IReadOnlyList<SecoSpellingVariant> Variants,
  bool IsOptional = false);

public sealed record SecoNameComposition(IReadOnlyList<string> Names, int IncompleteVariantSets);

// Builds full names from SECO name parts WITHOUT mixing scripts or variant sets.
//
// SECO gives each name part a primary value plus spelling variants (other
// transliterations and other scripts). Swapping one variant into the primary name
// produced names that never exist, e.g. "Lukashenka Dzmitry Александрович".
// Instead, variants are grouped by (script, language): one full name is composed
// per group, taking every part from that group. A part missing in the group may be
// filled from another variant of the SAME script, or from the primary value if that
// is in the same script. If a part cannot be expressed in the group's script, no
// name is emitted for the group (counted in IncompleteVariantSets): a partial name
// would raise false positives, a mixed-script name is not a real name.
public static class SecoNameComposer
{
  public const string Neutral = "NEUTRAL";
  public const string Mixed = "MIXED";

  public static SecoNameComposition Compose(IReadOnlyList<SecoNamePart> parts)
  {
    ArgumentNullException.ThrowIfNull(parts);
    var all = parts.Select((part, index) => (part, index))
      .OrderBy(x => x.part.Order).ThenBy(x => x.index)
      .Select(x => x.part)
      .Where(x => !string.IsNullOrWhiteSpace(x.Value) ||
        x.Variants.Any(v => !string.IsNullOrWhiteSpace(v.Value)))
      .ToArray();
    // Variant names are composed from the real name parts only; a title without a
    // spelling in another script must not block that script's name.
    var ordered = all.Where(x => !x.IsOptional).ToArray();
    if (ordered.Length == 0)
    {
      ordered = all;
    }
    var names = new List<string>();
    var seen = new HashSet<string>(StringComparer.Ordinal);
    void Emit(IEnumerable<string> values)
    {
      string name = string.Join(" ", values.Select(x => x.Trim()));
      if (name.Length > 0 && seen.Add(name))
      {
        names.Add(name);
      }
    }

    // 1. The primary name exactly as published (including titles), then without titles.
    Emit(all.Where(x => !string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Value!));
    Emit(ordered.Where(x => !string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Value!));

    // 2. One name per (script, language) variant set.
    // Ordinal key order keeps the CSV output deterministic across machine cultures.
    var groups = new SortedDictionary<(string Script, string Language), List<string>[]>(
      Comparer<(string Script, string Language)>.Create((a, b) =>
      {
        int result = string.CompareOrdinal(a.Script, b.Script);
        return result != 0 ? result : string.CompareOrdinal(a.Language, b.Language);
      }));
    for (int p = 0; p < ordered.Length; p++)
    {
      foreach (var variant in ordered[p].Variants)
      {
        if (string.IsNullOrWhiteSpace(variant.Value))
        {
          continue;
        }
        string script = NormalizedScript(variant.Value);
        if (script == Neutral)
        {
          continue;
        }
        var key = (script, (variant.Language ?? "").Trim().ToLowerInvariant());
        if (!groups.TryGetValue(key, out var byPart))
        {
          byPart = Enumerable.Range(0, ordered.Length).Select(_ => new List<string>()).ToArray();
          groups.Add(key, byPart);
        }
        byPart[p].Add(variant.Value.Trim());
      }
    }

    int incomplete = 0;
    foreach (var ((script, _), byPart) in groups)
    {
      // Several variants of one part in the same set (e.g. no language attribute)
      // are paired by position instead of multiplied out.
      int count = byPart.Max(x => x.Count);
      for (int i = 0; i < count; i++)
      {
        var values = new List<string>(ordered.Length);
        bool complete = true;
        for (int p = 0; p < ordered.Length; p++)
        {
          string? value = byPart[p].Count > 0 ? byPart[p][Math.Min(i, byPart[p].Count - 1)]
            : SameScriptFallback(ordered[p], script);
          if (value is null)
          {
            complete = false;
            break;
          }
          values.Add(value);
        }
        if (complete)
        {
          Emit(values);
        }
        else
        {
          incomplete++;
        }
      }
    }
    return new SecoNameComposition(names, incomplete);
  }

  // Another variant of the same script (any language), else the primary value if it
  // is written in that script. Script-neutral parts (digits, roman numerals in some
  // sources) fit any script.
  private static string? SameScriptFallback(SecoNamePart part, string script)
  {
    var variant = part.Variants
      .Where(v => !string.IsNullOrWhiteSpace(v.Value))
      .OrderBy(v => v.Language ?? "", StringComparer.Ordinal)
      .FirstOrDefault(v => NormalizedScript(v.Value) == script);
    if (variant is not null)
    {
      return variant.Value.Trim();
    }
    if (!string.IsNullOrWhiteSpace(part.Value))
    {
      string primary = NormalizedScript(part.Value);
      if (primary == script || primary == Neutral)
      {
        return part.Value.Trim();
      }
    }
    return null;
  }

  // Script after NameNormalizer, which repairs Latin/Cyrillic look-alike letters
  // (SECO contains e.g. "АНАНIЧ" with a Latin I). Grouping on the raw value would
  // split such a variant from the rest of its set.
  public static string NormalizedScript(string value) => DetectScript(NameNormalizer.Normalize(value));

  // Script of all letters in the value: LATIN, CYRILLIC, ARABIC, ..., NEUTRAL when it
  // contains no letters, MIXED when letters of more than one script occur.
  public static string DetectScript(string value)
  {
    string? found = null;
    foreach (char c in value)
    {
      if (!char.IsLetter(c))
      {
        continue;
      }
      string script = ScriptOf(c);
      if (found is null)
      {
        found = script;
      }
      else if (found != script)
      {
        return Mixed;
      }
    }
    return found ?? Neutral;
  }

  private static string ScriptOf(char c) => c switch
  {
    <= 'ɏ' => "LATIN",
    >= 'Ḁ' and <= 'ỿ' => "LATIN",
    >= 'Ͱ' and <= 'Ͽ' => "GREEK",
    >= 'ἀ' and <= '῿' => "GREEK",
    >= 'Ѐ' and <= 'ԯ' => "CYRILLIC",
    >= '԰' and <= '֏' => "ARMENIAN",
    >= '֐' and <= '׿' => "HEBREW",
    >= '؀' and <= 'ۿ' => "ARABIC",
    >= 'ݐ' and <= 'ݿ' => "ARABIC",
    >= 'ࢠ' and <= 'ࣿ' => "ARABIC",
    >= 'ﭐ' and <= '﷿' => "ARABIC",
    >= 'ﹰ' and <= '﻿' => "ARABIC",
    >= '฀' and <= '๿' => "THAI",
    >= 'Ⴀ' and <= 'ჿ' => "GEORGIAN",
    >= 'ᄀ' and <= 'ᇿ' => "HANGUL",
    >= '㄰' and <= '㆏' => "HANGUL",
    >= '가' and <= '힯' => "HANGUL",
    >= '぀' and <= 'ヿ' => "CJK",
    >= '㐀' and <= '䶿' => "CJK",
    >= '一' and <= '鿿' => "CJK",
    >= '豈' and <= '﫿' => "CJK",
    _ => "OTHER"
  };
}
