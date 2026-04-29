# FFXIVPatchGenerator

Builds a text patch release for the global FFXIV client by writing Korean EXD strings into the global language slot selected by `--target-language`.

Current scope:

- Reads global and Korean `0a0000.win32.index/index2/dat*`.
- Uses global `exd/root.exl` and global EXH structure.
- Rebuilds default-variant EXD pages for the target language, replacing only string columns with Korean SeString bytes.
- Writes a new `0a0000.win32.dat1`.
- Writes modified `0a0000.win32.index` and `0a0000.win32.index2`.
- Writes `orig.0a0000.win32.index`, `orig.0a0000.win32.index2`, and `ffxivgame.ver`.
- With `--include-font`, writes `000000.win32.dat1`, `000000.win32.index`, `000000.win32.index2`, and matching `orig.*` files.
- Font patching first uses the original generator-style TTMP font package (`TTMPD.mpd` + `TTMPL.mpl`) next to the executable or under `FontPatchAssets`.
- Never writes to either source game directory; all generated files go under `--output`.

Build on the current machine:

```powershell
.\build.ps1
```

Example:

```powershell
.\bin\Release\FFXIVPatchGenerator.exe `
  --global "D:\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game" `
  --korea "E:\FINAL FANTASY XIV - KOREA\game" `
  --target-language ja `
  --include-font `
  --output "E:\codex\release-ja"
```

If the global client is already patched and the `orig.*.index` file is missing, supply clean indexes with `--base-index` and `--base-font-index`. Matching `*.index2` files are detected next to those base indexes, or can be supplied with `--base-index2` and `--base-font-index2`. `--allow-patched-global` exists only for experiments because it will make the restore indexes non-original.

`ExcelVariant.Subrows` sheets are intentionally skipped for now. Font patching requires the TTMP font package because direct Korean client font copying can leave missing-glyph output in the global client. For test/release builds, keep `TTMPD.mpd` and `TTMPL.mpl` beside `FFXIVPatchGenerator.exe` or under `FFXIVPatchGenerator\FontPatchAssets`. The package files are intentionally ignored by Git and should be supplied only for local build/release packaging. `--allow-korean-font-fallback` exists only for experiments.

For safety, `--output` is rejected if it points inside either source game directory.
