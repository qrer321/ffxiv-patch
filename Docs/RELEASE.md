# Release Build Notes

Release binaries are intentionally not kept at the repository root.

Use:

```powershell
.\Scripts\build-release.ps1
```

The script builds:

- `FFXIVPatchGenerator\FFXIVPatchGenerator.csproj`
- `FFXIVPatchUI\FfxivPatchUi.sln`

Then it copies only the runtime files needed for distribution into:

```text
Release\Public\
```

Expected release files:

- `FFXIVKoreanPatch.exe`
- `FFXIVKoreanPatch.exe.config`
- `FFXIVPatchGenerator.exe`
- `FFXIVKoreanPatchUpdater.exe`
- `FFXIVKoreanPatchUpdater.exe.config`

Upload the contents of `Release\Public` to a GitHub Release. Do not commit `Release`.
