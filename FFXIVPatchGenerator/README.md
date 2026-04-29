# FFXIVPatchGenerator

Builds a text patch release for the global FFXIV client by writing Korean EXD strings into the global language slot selected by `--target-language`.

Current scope:

- Reads global and Korean `0a0000.win32.index/dat*`.
- Uses global `exd/root.exl` and global EXH structure.
- Rebuilds default-variant EXD pages for the target language, replacing only string columns with Korean SeString bytes.
- Writes a new `0a0000.win32.dat1`.
- Writes a modified `0a0000.win32.index`.
- Writes `orig.0a0000.win32.index` and `ffxivgame.ver`.
- With `--include-font`, writes `000000.win32.dat1`, `000000.win32.index`, and `orig.000000.win32.index`.
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

If the global client is already patched and the `orig.*.index` file is missing, supply clean indexes with `--base-index` and `--base-font-index`. `--allow-patched-global` exists only for experiments because it will make the restore indexes non-original.

`ExcelVariant.Subrows` sheets are intentionally skipped for now. Font patching copies known Korean `common/font` resources from `000000` into a new global `000000.win32.dat1`.

For safety, `--output` is rejected if it points inside either source game directory.
