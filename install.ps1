$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Após publicar o EXE em uma release, altere apenas estas três linhas.
$Repo = 'Nata-Felix/Instalador_VS-_visual'
$Release = 'v1.3.0'
$Asset = 'VisualCppInstaller.exe'

$BaseUrl = "https://github.com/$Repo/releases/download/$Release"
$PastaTemporaria = Join-Path ([IO.Path]::GetTempPath()) ("VisualCppInstaller_{0}" -f [guid]::NewGuid().ToString('N'))
$Destino = Join-Path $PastaTemporaria $Asset
$PastaPacotes = Join-Path $PastaTemporaria 'packages'
$PacotesDaRelease = @(
    'vc2008_x86.exe',
    'vc2008_x64.exe',
    'vc2012_x86.exe',
    'vc2012_x64.exe',
    'vc14_x86.exe',
    'vc14_x64.exe'
)

try {
    New-Item -ItemType Directory -Path $PastaPacotes -Force | Out-Null
    Write-Host 'Baixando a interface do instalador Microsoft Visual C++...'
    $Client = New-Object Net.WebClient
    $Client.Headers['User-Agent'] = 'VisualCppInstaller'
    $Client.DownloadFile("$BaseUrl/$Asset", $Destino)

    if (!(Test-Path -LiteralPath $Destino) -or (Get-Item -LiteralPath $Destino).Length -lt 1024) {
        throw 'O executável baixado é inválido.'
    }

    foreach ($Pacote in $PacotesDaRelease) {
        try {
            Write-Host "Baixando pacote local: $Pacote"
            $Client.DownloadFile("$BaseUrl/$Pacote", (Join-Path $PastaPacotes $Pacote))
        }
        catch {
            Write-Warning "O pacote $Pacote não estava na release. A interface usará o link oficial da Microsoft."
        }
    }

    $Processo = Start-Process -FilePath $Destino -Verb RunAs -Wait -PassThru
    exit $Processo.ExitCode
}
finally {
    Remove-Item -LiteralPath $PastaTemporaria -Recurse -Force -ErrorAction SilentlyContinue
}
