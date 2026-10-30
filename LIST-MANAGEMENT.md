# flix list manager

`Flix.ListManager` is a Windows Forms desktop application targeting `net10.0-windows`. Run it in Visual Studio on Windows (set as Startup Project) or from PowerShell:

```powershell
dotnet run --project .\src\Flix.ListManager
```

Use the **Blacklist** tab to open `reference-data/blacklist.json`, edit individual entries, search, multi-select, activate/deactivate and save. Use the **Whitelist groups** tab to open `reference-data/whitelist.json`, manage groups and edit the selected group's criteria in the lower grid. The grids allow direct editing of properties. The identifier fields `Id` are hidden and retained for stable file identity.

The JSON files are external management data, not Lucene indexes. To publish a new immutable snapshot after saving, run `import-bundle.ps1`. Only active blacklist entries are indexed. Whitelist groups are indexed only when the group **and every criterion inside it** are active. This conservative rule prevents removing part of a whitelist group from accidentally broadening an approval. Groups remain bound by `(EntityId, SourceId)` as required by the existing whitelist evaluator. A group must contain at least one criterion, and identifiers must be unique.

CSV import is still accepted for existing deployments, but CSV has no enable/disable flag. The import script uses the managed JSON files by default.

**Safety:** This is an administrative editing tool. Add role-based access, change approval, tamper-evident history and controlled distribution before using it in a regulated production environment. Writing JSON does **not** modify the active screening index; importing and publishing a new snapshot is a separate operation.
