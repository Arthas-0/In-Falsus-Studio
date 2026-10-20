# Build Visual & UI 1.0.39

The plugin source targets the Windows Mono 1.0.10 editor and its BepInEx installation. Build on Windows with PowerShell, the matching editor directory, and a Unity 2022.3.8f1c1 installation containing Unity's bundled `dotnet.exe` and Roslyn `csc.dll`.

From the repository root:

```powershell
.\Mods\VisualUI\1.0.39\Build.ps1 `
  -Editor 'C:\Games\InFalsusStudio_1.0.10' `
  -UnityEditor 'C:\Program Files\Unity\Hub\Editor\2022.3.8f1c1'
```

The target directory must contain `InFalsusStudio_Data/Managed` and `BepInEx/core`. The compiler is read from `UnityEditor/Editor/Data/NetCoreRuntime/dotnet.exe` and `UnityEditor/Editor/Data/DotNetSdkRoslyn/csc.dll`. The resulting `InfStudioVisualUI.dll` is written under `Source/`; it is intentionally ignored by Git.

The three PowerShell tools in `ExternalTools/` run through Windows PowerShell and .NET Framework, already available on supported Windows installations. Place the three directories under the editor-root `ExternalTools/` to use them. Additional tools are not included in this source checkpoint.

This repository records the source and build procedure only. The separately prepared local integrated package also contains runtime dependencies and game-derived resources, which are not redistributed here.
