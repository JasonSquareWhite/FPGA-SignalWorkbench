param([string]$Python='python', [switch]$SkipReference)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    & .\build.ps1
    function Run-App([string]$mode,[string]$file) {
        $proc=Start-Process -FilePath (Join-Path $root 'SignalWorkbench.exe') -ArgumentList $mode,$file -WorkingDirectory $root -WindowStyle Hidden -Wait -PassThru
        if($proc.ExitCode -ne 0){throw "Application check failed: $mode (see $file.error.txt)"}
    }
    Run-App '--self-test' 'tests/results'
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    foreach($name in @('FilterChecks','BoundaryChecks')) {
        & $compiler /nologo /target:exe "/out:$root\$name.exe" "/reference:$root\SignalWorkbench.exe" /reference:System.Numerics.dll "$root\tests\$name.cs"
        if($LASTEXITCODE -ne 0){throw "Compilation failed: $name"}
    }
    & .\FilterChecks.exe tests/results/filter-reference
    if($LASTEXITCODE -ne 0){throw 'Filter checks failed'}
    & .\BoundaryChecks.exe tests/results/boundaries.csv
    if($LASTEXITCODE -ne 0){throw 'Boundary checks failed'}
    if(!$SkipReference) {
        Run-App '--reference' 'tests/results/reference.csv'
        & $Python tests/verify_reference.py tests/results/reference.csv
        if($LASTEXITCODE -ne 0){throw 'Window/wave independent reference failed'}
        & $Python tests/verify_filters.py tests/results/filter-reference
        if($LASTEXITCODE -ne 0){throw 'Filter independent reference failed'}
        & $Python tests/verify_boundaries.py tests/results/boundaries.csv
        if($LASTEXITCODE -ne 0){throw 'Boundary independent reference failed'}
    }
    Write-Output 'PASS: all requested checks'
} finally {Pop-Location}
