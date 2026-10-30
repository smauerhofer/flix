$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "src/Flix.Cli"
$dataRoot = Join-Path $root "flix-data"

dotnet run --project $project -- import-bundle $dataRoot `
  (Join-Path $root "sample-data/fields.csv") `
  (Join-Path $root "reference-data/blacklist.json") `
  (Join-Path $root "reference-data/whitelist.json") `
  (Join-Path $root "reference-data/tenants")
if ($LASTEXITCODE -ne 0)
{
  throw "Bundle import failed: $LASTEXITCODE"
}
