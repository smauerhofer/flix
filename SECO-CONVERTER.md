# SECO full-list converter

The source `consolidated-list_2026-10-05.zip` is a `whole-list` export dated 2026-10-05. The command reads a ZIP containing exactly one XML file, or a plain XML document:

```powershell
dotnet run --project .\src\Flix.Cli -- convert-seco .\consolidated-list_2026-10-05.zip .\reference-data\seco-normal.csv
```

The output schema is `sourceId,entityId,type,value`, where sourceId is SECO and entityId is the SECO target SSID, and type is PERSON_NAME, ORGANIZATION_NAME or ADDRESS. COUNTRY rows are intentionally not emitted: a country attached to a SECO target describes that target (address, nationality) and is not an embargo. Country shutdowns are maintained explicitly as COUNTRY entries in the global or tenant blacklist (see `SIDE-SCREENING.md`). The country name remains part of the ADDRESS text. Distinct spellings and names are emitted as separate fields for the same SSID.

**Spelling variants** (`SecoNameComposer`): each name is emitted as published, plus one full name per variant set, where a set is all `spelling-variant` elements of the same script (detected from the characters) and the same `lang` attribute. Example: `Lukashenka Dzmitry Aliaksandravich`, `Lukashenko Dmitri Aleksandrovich`, `Лукашенко Дмитрий Александрович`, `Лукашэнка Дзмітрый Аляксандравіч`. A part missing from a set is taken from another variant or the primary value **of the same script**; if none exists the set is skipped (reported as "variant sets skipped"). Variants of different scripts are never combined, so mixed-script names such as `Lukashenka Dzmitry Александрович` are not produced. Earlier converter versions substituted one variant at a time and produced such names (37% of the 2026-10-05 name rows); CSVs created with them should be regenerated from the XML. Sanctions-set membership is not split into new entity IDs: a target may belong to multiple sanctions sets. This CSV is intended for the normal reference index.

**Limitations:** The CSV format does not retain sanctions-set IDs, historical modification timelines, dates of birth, documents, or complete structured address metadata. Conversion skips targets whose final modification is de-listed, but the SECO authoritative interpretation of the full-list membership should be independently validated before production deployment. No regulatory interpretation should be inferred solely from historical modification records. The initial CSV does not represent every category of SECO XML data. The generated CSV and engine matching should be validated on the full list.

**Important:** `convert-seco` refuses incremental amendment exports. Always convert a `whole-list` source when replacing the normal index. The output is written via a temporary file then renamed after successful conversion.

Import indexes using the existing `import-bundle` command after generating the CSV.
