# Unity プロジェクトの embedded package をこのリポジトリへ同期する
param(
    [string]$UnityProject = "C:\Users\rroki\Keybase\vrchat-avatars",
    [string]$PackageId = "jp.rroki.ttt-poiyomi-support"
)

$src = Join-Path $UnityProject "Packages\$PackageId"
$dst = Join-Path (Split-Path $PSScriptRoot -Parent) "Packages\$PackageId"

if (-not (Test-Path (Join-Path $src "package.json"))) {
    Write-Error "package.json not found: $src"
    exit 1
}

robocopy $src $dst /MIR /NFL /NDL /NJH /NJS
if ($LASTEXITCODE -le 7) {
    Write-Host "synced: $src -> $dst"
    exit 0
}
exit $LASTEXITCODE
