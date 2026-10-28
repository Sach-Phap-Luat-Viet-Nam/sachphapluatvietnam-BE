<#
.SYNOPSIS
    Backup CSDL PostgreSQL (container docker compose trong WSL) ra file .dump, xoa ban cu hon KeepDays ngay.

.DESCRIPTION
    Huong dan va cach khoi phuc: docs/deploy-windows.md. register-tasks.ps1 dang ky chay hang ngay.
    File chi dung ky tu ASCII vi Windows PowerShell 5.1 doc file UTF-8 khong BOM theo bang ma ANSI.

.EXAMPLE
    .\deploy\windows\backup-db.ps1 -BackupDir 'D:\Backups\SachLuat'
#>
[CmdletBinding()]
param(
    [string] $Distro = 'Ubuntu',

    [string] $Container = 'sachluat-postgres-1',

    [string] $Database = 'sachluat',

    [string] $User = 'sachluat',

    [string] $BackupDir = 'C:\Backups\SachLuat',

    [int] $KeepDays = 14
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
$target = Join-Path ([IO.Path]::GetFullPath($BackupDir)) ('sachluat-{0:yyyyMMdd-HHmmss}.dump' -f (Get-Date))

$wslTarget = (& wsl.exe -d $Distro -- wslpath -a ($target -replace '\\', '/'))
if ($LASTEXITCODE -ne 0 -or -not $wslTarget) {
    throw "Khong doi duoc duong dan $target sang WSL (distro $Distro co dang chay khong?)."
}

# Ghi file tu ben trong WSL: dua du lieu nhi phan qua pipe cua PowerShell 5.1 se lam hong file dump.
# pg_dump chay trong container qua Unix socket nen khong can mat khau.
$command = "docker exec $Container pg_dump -U $User -d $Database --format=custom > '$($wslTarget.Trim())'"
& wsl.exe -d $Distro -- sh -c $command
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $target) -or (Get-Item $target).Length -eq 0) {
    Remove-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue
    throw "pg_dump loi (exit $LASTEXITCODE)."
}
Write-Output "Da backup: $target ($([math]::Round((Get-Item $target).Length / 1KB)) KB)"

$expired = Get-ChildItem -LiteralPath $BackupDir -Filter 'sachluat-*.dump' |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) }
foreach ($file in $expired) {
    Remove-Item -LiteralPath $file.FullName -Force
    Write-Output "Da xoa ban cu: $($file.Name)"
}
