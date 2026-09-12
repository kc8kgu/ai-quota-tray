[CmdletBinding()]
param(
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "src\AIQuotaTray\AIQuotaTray.csproj"
$output = Join-Path $repositoryRoot "artifacts\publish"
$resolvedRepositoryRoot = [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd('\') + '\'
$resolvedOutput = [System.IO.Path]::GetFullPath($output)

if (-not $resolvedOutput.StartsWith($resolvedRepositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean a publish directory outside the repository: $resolvedOutput"
}

if (Test-Path -LiteralPath $resolvedOutput) {
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

dotnet publish $project `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $output

Write-Host "Published AIQuotaTray to $output"
