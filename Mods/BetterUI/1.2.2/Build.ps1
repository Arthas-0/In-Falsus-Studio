param([Parameter(Mandatory=$true)][string]$Editor)
$ErrorActionPreference='Stop'
$taskManaged=Join-Path $Editor 'InFalsusStudio_Data\Managed'
$taskCore=Join-Path $Editor 'BepInEx\core'
$taskRefs=@('Assembly-CSharp.dll','UnityEngine.CoreModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.TextRenderingModule.dll','UnityEngine.InputLegacyModule.dll','UnityEngine.AudioModule.dll','UnityEngine.ScreenCaptureModule.dll','UnityEngine.JSONSerializeModule.dll','UnityEngine.ImageConversionModule.dll','UnityEngine.dll','netstandard.dll')|ForEach-Object{'/reference:'+(Join-Path $taskManaged $_)}
$taskRefs+=@('BepInEx.Core.dll','BepInEx.Unity.Mono.dll','0Harmony.dll')|ForEach-Object{'/reference:'+(Join-Path $taskCore $_)}
$taskSources=@('NativeEditorUIPlugin.cs','NativeEditorUIPanels.cs','NativeEditorUIDocked.cs','NativeEditorUIMatched.cs','NativeEditorUIFilters.cs','NativeEditorUIInteraction.cs','NativeEditorUIPopup.cs','NativeEditorUIRefinements.cs','NativeEditorUILocalization.cs')|ForEach-Object{Join-Path $PSScriptRoot $_}
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:library ('/out:'+(Join-Path $PSScriptRoot 'NativeEditorUI.dll')) $taskRefs $taskSources
if($LASTEXITCODE -ne 0){throw 'Native editor UI compilation failed'}
