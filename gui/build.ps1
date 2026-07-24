$ErrorActionPreference = 'Stop'

$GuiDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $GuiDir
$OutputExe = Join-Path $ProjectDir 'VisualCppInstaller.exe'
$Source = Join-Path $GuiDir 'VisualCppInstallerGui.cs'
$Manifest = Join-Path $GuiDir 'app.manifest'
$Logo = Join-Path $GuiDir 'assets\logo_display.png'
$Icon = Join-Path $GuiDir 'assets\VisualCppInstaller.ico'

$Candidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$Csc = $Candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (!$Csc) { throw 'Compilador C# do .NET Framework 4 não encontrado.' }

& $Csc /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:$Manifest `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /resource:"$Logo",VisualCppLogo /resource:"$Icon",VisualCppIcon `
    /win32icon:$Icon /out:$OutputExe $Source

if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $OutputExe)) {
    throw 'Falha ao compilar VisualCppInstaller.exe.'
}

$Version = [Diagnostics.FileVersionInfo]::GetVersionInfo($OutputExe).FileVersion
Write-Host "Criado: $OutputExe"
Write-Host "Versão: $Version"
