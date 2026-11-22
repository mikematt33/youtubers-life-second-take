param([Parameter(Mandatory)][string]$GameRoot)
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GameRoot 'YoutubersLife_Data\Managed'
$names = 'mscorlib','System','System.Core','netstandard','UnityEngine','UnityEngine.CoreModule','UnityEngine.UIModule','UnityEngine.InputLegacyModule','Assembly-CSharp'
$refs = foreach ($name in $names) { '/r:' + (Join-Path $managed "$name.dll") }
$refs += '/r:' + (Join-Path $GameRoot 'BepInEx\core\BepInEx.dll')
$output = Join-Path $PSScriptRoot 'bin'
New-Item -Path $output -ItemType Directory -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /noconfig /nostdlib+ /optimize+ /target:library ("/out:" + (Join-Path $output 'SecondTake.Visuals.dll')) @refs (Join-Path $PSScriptRoot 'BedroomVisuals.cs')
if ($LASTEXITCODE -ne 0) { throw "Visuals build failed with exit code $LASTEXITCODE." }
