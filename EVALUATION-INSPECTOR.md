# FLIX Evaluation Inspector

`Flix.EvaluationInspector` is a Windows Forms GUI for inspecting FLIX positive-evaluation CSV/JSON reports. It can optionally join the originating positive-case JSONL by `caseId` to show the incoming screening value.

Run on Windows:

```powershell
dotnet run --project .\src\Flix.EvaluationInspector -- .\reports\seco-fuzzy-improved.csv .\test-data\seco-fuzzy.jsonl
```

Alternatively open the solution in Visual Studio and select `Flix.EvaluationInspector` as the startup project. Use **Browse report**, **Browse cases**, and **Load**.

Features include searchable case table; decision, tenant, strategy, diagnostic, and missing-entity filters; sortable columns; full case detail; latency; and **Next failure (F8)** navigation. CSV files with quoted fields and full JSON report files are supported.

This version does not invent original, pre-mutation reference values, actual candidate lists, or per-algorithm scores. Those are not available in the existing positive-case and evaluator formats. Case JSONL contains the incoming (possibly mutated) value and expected identifiers.

The application is read-only and does not modify snapshots or screening decisions.
