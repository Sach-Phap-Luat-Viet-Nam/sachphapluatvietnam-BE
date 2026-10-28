#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Dang ky 2 Scheduled Task: giu WSL luon chay (PostgreSQL, Redis) va backup CSDL hang ngay.

.DESCRIPTION
    Ca hai task chay duoi tai khoan Windows hien tai (distro WSL cai theo tung tai khoan), ke ca khi
    chua dang nhap, nen script hoi mat khau tai khoan. Huong dan: docs/deploy-windows.md.
    File chi dung ky tu ASCII vi Windows PowerShell 5.1 doc file UTF-8 khong BOM theo bang ma ANSI.

.EXAMPLE
    .\deploy\windows\register-tasks.ps1 -BackupDir 'D:\Backups\SachLuat'
#>
[CmdletBinding()]
param(
    [string] $Distro = 'Ubuntu',

    [string] $BackupDir = 'C:\Backups\SachLuat',

    [string] $BackupTime = '02:00',

    [int] $KeepDays = 14
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$userName = "$env:USERDOMAIN\$env:USERNAME"
$credential = Get-Credential -UserName $userName -Message 'Mat khau Windows de task chay ca khi chua dang nhap'
$password = $credential.GetNetworkCredential().Password

# WSL tat distro khi khong con tien trinh wsl.exe nao, keo PostgreSQL va Redis tat theo:
# task nay giu mot tien trinh sleep chay mai. Khong gioi han thoi gian chay, tu chay lai neu bi tat.
$keepAliveSettings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -RestartCount 999 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName 'SachLuat WSL keep-alive' `
    -Description 'Giu WSL chay de PostgreSQL va Redis (docker compose) luon san sang cho SachLuatApi.' `
    -Action (New-ScheduledTaskAction -Execute 'wsl.exe' -Argument "-d $Distro --exec sleep infinity") `
    -Trigger (New-ScheduledTaskTrigger -AtStartup) `
    -Settings $keepAliveSettings `
    -User $userName `
    -Password $password `
    -Force | Out-Null
Start-ScheduledTask -TaskName 'SachLuat WSL keep-alive'
Write-Host 'Da dang ky va chay task: SachLuat WSL keep-alive'

$backupScript = Join-Path $PSScriptRoot 'backup-db.ps1'
$backupArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$backupScript`" " +
    "-Distro $Distro -BackupDir `"$BackupDir`" -KeepDays $KeepDays"
$backupSettings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Hours 1) `
    -StartWhenAvailable `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName 'SachLuat DB backup' `
    -Description "pg_dump CSDL sachluat vao $BackupDir, giu $KeepDays ngay." `
    -Action (New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $backupArguments) `
    -Trigger (New-ScheduledTaskTrigger -Daily -At $BackupTime) `
    -Settings $backupSettings `
    -User $userName `
    -Password $password `
    -Force | Out-Null
Write-Host "Da dang ky task: SachLuat DB backup (hang ngay luc $BackupTime)"
