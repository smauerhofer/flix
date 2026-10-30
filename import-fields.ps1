# Imports a complete three-index snapshot. Use import-bundle.ps1 directly as well.
$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "import-bundle.ps1")
if ($LASTEXITCODE -ne 0)
{
  throw "Bundle import failed: $LASTEXITCODE"
}
