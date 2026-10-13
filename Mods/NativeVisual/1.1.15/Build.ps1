param([Parameter(Mandatory=$true)][string]$Editor)
$ErrorActionPreference='Stop'
$taskManaged=Join-Path $Editor 'InFalsusStudio_Data\Managed'
$taskCore=Join-Path $Editor 'BepInEx\core'
$taskRefs=@('UnityEngine.CoreModule.dll','UnityEngine.AssetBundleModule.dll','UnityEngine.ImageConversionModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.JSONSerializeModule.dll','UnityEngine.dll','netstandard.dll','Unity.RenderPipelines.Core.Runtime.dll','Unity.RenderPipelines.Universal.Runtime.dll') | ForEach-Object { '/reference:'+(Join-Path $taskManaged $_) }
$taskRefs+=@('BepInEx.Core.dll','BepInEx.Unity.Mono.dll','0Harmony.dll') | ForEach-Object { '/reference:'+(Join-Path $taskCore $_) }
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:library ("/out:"+(Join-Path $PSScriptRoot 'NativeVisualMod.dll')) $taskRefs (Join-Path $PSScriptRoot 'NativeVisualPlugin.cs') (Join-Path $PSScriptRoot 'NativeVisualTrack.cs') (Join-Path $PSScriptRoot 'NativeVisualTests.cs') (Join-Path $PSScriptRoot 'NativeVisualPresentation.cs') (Join-Path $PSScriptRoot 'NativeVisualAir.cs') (Join-Path $PSScriptRoot 'NativeVisualCenter.cs')
if($LASTEXITCODE -ne 0){throw 'Compilation failed'}
Write-Host ('Built: '+(Join-Path $PSScriptRoot 'NativeVisualMod.dll'))
