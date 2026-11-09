# Kitten-Engineer-Redux
Source code for the Kitten Engineer Redux mod for Kitten Space Agency

Compatibility target: **KSA 2026.11.10.5801**, **StarMap 0.5.4**, and **.NET 10**.

The previous checked-in ZIP contained an older DLL referencing the retired
`Brutal.Core.Common`, `Brutal.Core.Logging`, and `Brutal.Core.Numerics` assemblies.
Updating the source package references alone does not repair that binary. The
replacement ZIP is rebuilt against the current game assemblies.

The UI patches now match KSA's `IGameViewport` arguments. StarMap's menu API
invokes an action when clicked, so the Flight HUD and Editor Panel each have a
separate toggle under **Mods**. Deployment includes the manifest and dependency
metadata alongside the DLL; game and loader assemblies are supplied by the host.

Build with the .NET 10 SDK, pointing `KsaDirectory` at the current game's DLLs:

```powershell
dotnet build KittenEngineerRedux.slnx -c Release "-p:KsaDirectory=C:\Program Files\Ahwoo\Games\Kitten Space Agency"
```

Output is in `bin/Release/net10.0/`. To copy the three required files directly
to a mod folder, also pass `"-p:ModDeployDir=C:\path\to\mods\KittenEngineerRedux"`.
Deployment is optional and no longer assumes a particular Windows user account.

To install, replace the previous `KittenEngineerRedux` mod folder with the
contents of `dist/KittenEngineerRedux.zip`. The folder must be named
`KittenEngineerRedux` and contain `mod.toml`, `KittenEngineerRedux.dll`, and
`KittenEngineerRedux.deps.json`. Enable that mod ID in KSA's active manifest and
restart through StarMap 0.5.4. Avoid installing the same ID in both the game's
`Content` directory and the user `mods` directory.

Validation: Release build against the target game DLLs and published StarMap API
0.5.4, metadata checks of the patch/lifecycle signatures, and deployment/archive
contents. An in-game launch is still required to confirm the editor, flight HUD,
and both menu toggles; the x64 game cannot run in the ARM Mac build environment.

IMPORTANT NOTE:
Generative AI has been used in this project to automatically search through ILSpy. The majority of the code in this repository is human written.
