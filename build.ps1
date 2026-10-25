$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output /win32manifest:"$root\src\app.manifest" /out:"$root\SignalWorkbench.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Numerics.dll /reference:System.Xml.dll "$root\src\Engine.cs" "$root\src\MainForm.cs" "$root\src\Program.cs" "$root\src\Filters.cs" "$root\src\FilterUI.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Copy-Item -LiteralPath (Join-Path $root 'src\app.config') -Destination (Join-Path $root 'SignalWorkbench.exe.config') -Force
Get-Item -LiteralPath (Join-Path $root 'SignalWorkbench.exe') | Select-Object Name, Length
