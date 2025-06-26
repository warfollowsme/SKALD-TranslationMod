param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

# Check version format
if (-not ($Version -match '^\d+\.\d+\.\d+$')) {
    Write-Error "Version should be in X.Y.Z format (e.g., 1.0.0)"
    exit 1
}

Write-Host "Updating version to $Version..."

# Update version in csproj file
$csprojPath = "src/TranslationMod.csproj"
if (Test-Path $csprojPath) {
    $content = Get-Content $csprojPath -Raw
    $content = $content -replace '<Version>[\d\.]+</Version>', "<Version>$Version</Version>"
    Set-Content $csprojPath $content -NoNewline
    Write-Host "✓ Updated version in $csprojPath"
} else {
    Write-Warning "File $csprojPath not found"
}

# Update version in language packs
Get-ChildItem "languages" -Directory | Where-Object { $_.Name -ne "template" } | ForEach-Object {
    $langPackPath = Join-Path $_.FullName "language_pack.json"
    if (Test-Path $langPackPath) {
        $content = Get-Content $langPackPath -Raw
        $json = $content | ConvertFrom-Json
        $json.version = $Version
        $content = $json | ConvertTo-Json -Depth 10 -Compress:$false
        Set-Content $langPackPath $content -NoNewline
        Write-Host "✓ Updated version in $langPackPath"
    }
}

Write-Host "Version successfully updated to $Version"
Write-Host "Now you can create tag and push:"
Write-Host "  git add ."
Write-Host "  git commit -m 'Release v$Version'"
Write-Host "  git tag v$Version"
Write-Host "  git push origin main --tags" 