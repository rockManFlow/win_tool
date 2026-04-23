# Prepares embedded Python 3.10.0 amd64 + pdf2docx==0.5.8 under this directory.
# Run from tools\python310\:  powershell -ExecutionPolicy Bypass -File .\install-bundled.ps1

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $here

$ver = "3.10.0"
$zipName = "python-$ver-embed-amd64.zip"
$url = "https://www.python.org/ftp/python/$ver/$zipName"

Write-Host "Downloading $url ..."
Invoke-WebRequest -Uri $url -OutFile $zipName -UseBasicParsing

Write-Host "Extracting (overwrite) ..."
Expand-Archive -Path $zipName -DestinationPath $here -Force
Remove-Item $zipName -Force

$pth = Join-Path $here "python310._pth"
if (-not (Test-Path $pth)) { throw "Missing python310._pth after extract." }

$pthContent = Get-Content $pth -Raw
if ($pthContent -notmatch "(?m)^import site\s*$") {
    Add-Content -Path $pth -Value "`nimport site`n"
    Write-Host "Appended 'import site' to python310._pth"
}

$getPip = Join-Path $here "get-pip.py"
Write-Host "Downloading get-pip.py ..."
Invoke-WebRequest -Uri "https://bootstrap.pypa.io/get-pip.py" -OutFile $getPip -UseBasicParsing

$py = Join-Path $here "python.exe"
& $py $getPip
Remove-Item $getPip -Force

$req = Join-Path $here "requirements.txt"
& $py -m pip install -r $req

Write-Host "Done. Test: $py -c `"import pdf2docx; print(pdf2docx.__version__)`""
