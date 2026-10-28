#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Publish SachLuat.Api va cai dat (hoac cap nhat) Windows Service. Chay lai moi lan deploy ban moi.

.DESCRIPTION
    Huong dan day du: docs/deploy-windows.md.
    File chi dung ky tu ASCII vi Windows PowerShell 5.1 doc file UTF-8 khong BOM theo bang ma ANSI.

.EXAMPLE
    .\deploy\windows\deploy.ps1 -EnvFile "$env:USERPROFILE\sachluat\service.env"
#>
[CmdletBinding()]
param(
    # File KEY=VALUE chua cau hinh production (mau: deploy/windows/service.env.example).
    [Parameter(Mandatory)]
    [string] $EnvFile,

    [string] $InstallDir = 'C:\Services\SachLuat.Api',

    [string] $ServiceName = 'SachLuatApi',

    # API chi nghe tren 127.0.0.1; cloudflared tren cung may chuyen request tu internet vao cong nay.
    [int] $Port = 5080,

    # Dung lai ban publish san co trong artifacts/, chi cap nhat cau hinh va khoi dong lai service.
    [switch] $SkipPublish
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $repoRoot 'src\SachLuat.Api\SachLuat.Api.csproj'
$publishDir = Join-Path $repoRoot 'artifacts\publish\SachLuat.Api'
$exeName = 'SachLuat.Api.exe'
# Trung ApplicationName: AddWindowsService ghi log Warning+ vao Event Log voi ten nguon nay.
$eventSource = 'SachLuat.Api'
$requiredKeys = 'ConnectionStrings__Default', 'ConnectionStrings__Redis', 'Jwt__SigningKey'

function Read-EnvFile([string] $path) {
    $vars = [ordered]@{}
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $path -Encoding UTF8) {
        $lineNumber++
        $trimmed = $line.Trim()
        if ($trimmed -eq '' -or $trimmed.StartsWith('#')) {
            continue
        }

        $parts = $trimmed -split '=', 2
        if ($parts.Count -ne 2 -or $parts[0].Trim() -eq '') {
            # Khong in noi dung dong: co the chua secret.
            throw "Dong $lineNumber trong $path khong dung dang KEY=VALUE."
        }

        # Bo qua bien de trong de app dung gia tri mac dinh thay vi chuoi rong.
        $value = $parts[1].Trim()
        if ($value -ne '') {
            $vars[$parts[0].Trim()] = $value
        }
    }

    return $vars
}

function Invoke-Sc([string] $arguments) {
    # Start-Process truyen nguyen chuoi lenh, giu duoc password= "" ma PowerShell 5.1 se bo mat.
    $process = Start-Process -FilePath 'sc.exe' -ArgumentList $arguments -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "sc.exe $arguments loi (exit $($process.ExitCode))."
    }
}

$envVars = Read-EnvFile $EnvFile
$missing = @($requiredKeys | Where-Object { -not $envVars.Contains($_) })
if ($missing.Count -gt 0) {
    throw "Thieu bien bat buoc trong ${EnvFile}: $($missing -join ', ')."
}
if (-not $envVars.Contains('ASPNETCORE_ENVIRONMENT')) {
    $envVars['ASPNETCORE_ENVIRONMENT'] = 'Production'
}
$envVars['ASPNETCORE_URLS'] = "http://127.0.0.1:$Port"

$InstallDir = [IO.Path]::GetFullPath($InstallDir)
if ([IO.Path]::GetPathRoot($InstallDir) -eq $InstallDir) {
    throw "InstallDir khong duoc la thu muc goc o dia: $InstallDir."
}
# robocopy /MIR xoa moi file la trong InstallDir: chi cho phep thu muc rong hoac ban cai cu.
if ((Test-Path $InstallDir) -and
    (Get-ChildItem -LiteralPath $InstallDir -Force | Select-Object -First 1) -and
    -not (Test-Path (Join-Path $InstallDir $exeName))) {
    throw "$InstallDir da co file nhung khong phai ban cai SachLuat.Api. Chon thu muc khac."
}

if (-not $SkipPublish) {
    if (Test-Path $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }
    Write-Host "Publish $project -> $publishDir"
    dotnet publish $project -c Release -r win-x64 --self-contained -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish loi (exit $LASTEXITCODE)."
    }
}
if (-not (Test-Path (Join-Path $publishDir $exeName))) {
    throw "Khong thay $exeName trong $publishDir. Chay lai khong co -SkipPublish."
}

if (-not [Diagnostics.EventLog]::SourceExists($eventSource)) {
    New-EventLog -LogName Application -Source $eventSource
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -ne 'Stopped') {
    Write-Host "Dung service $ServiceName"
    Stop-Service -Name $ServiceName -Force
}

Write-Host "Copy ban publish -> $InstallDir"
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
robocopy $publishDir $InstallDir /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) {
    throw "robocopy loi (exit $LASTEXITCODE)."
}

if (-not $service) {
    Write-Host "Tao service $ServiceName"
    New-Service -Name $ServiceName `
        -BinaryPathName "`"$(Join-Path $InstallDir $exeName)`"" `
        -DisplayName 'Sach Luat API' `
        -Description 'Backend Sach Luat Viet Nam (ASP.NET Core). Huong dan: docs/deploy-windows.md' `
        -StartupType Automatic | Out-Null
}

# Virtual account NT SERVICE\<ten>: quyen thap hon LocalSystem, khong can quan ly mat khau.
$account = "NT SERVICE\$ServiceName"
Invoke-Sc "config $ServiceName start= delayed-auto obj= `"$account`" password= `"`""
# Luc khoi dong may, WSL (PostgreSQL, Redis) co the chua san sang nen API thoat loi: tu chay lai.
Invoke-Sc "failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000"
Invoke-Sc "failureflag $ServiceName 1"

& icacls $InstallDir /grant "${account}:(OI)(CI)RX" /Q | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "icacls loi (exit $LASTEXITCODE)."
}

# Bien moi truong rieng cua service (SCM doc gia tri Environment khi khoi dong tien trinh).
$serviceKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
$lines = [string[]]@($envVars.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" })
New-ItemProperty -Path $serviceKey -Name 'Environment' -PropertyType MultiString -Value $lines -Force | Out-Null

Write-Host "Khoi dong service $ServiceName"
Start-Service -Name $ServiceName

$healthUrl = "http://127.0.0.1:$Port/health/ready"
$deadline = (Get-Date).AddSeconds(90)
$lastError = 'chua co phan hoi'
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3
    try {
        $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            Write-Host "Xong: $healthUrl tra 200."
            return
        }
    }
    catch [System.Net.WebException] {
        # API dang khoi dong (ap migration) hoac chua ket noi duoc CSDL/Redis: thu lai lan sau.
        $lastError = $_.Exception.Message
    }
}

throw "Service da chay nhung $healthUrl chua tra 200 sau 90 giay ($lastError). " +
    "Xem Event Viewer > Windows Logs > Application, nguon $eventSource."
