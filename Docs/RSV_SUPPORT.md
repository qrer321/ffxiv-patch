# RSV token support

## Current behavior

- The generator accepts `--rsv-map <file>` for a flat JSON object mapping RSV keys to text values.
- If `--rsv-map` is not supplied, the generator auto-detects `rsv.json` beside `FFXIVPatchGenerator.exe`, then `rsv.json` in the current working directory.
- The RSV map is not bundled in this repository. Update the external `rsv.json` when new content adds RSV keys.
- Release builds download the current RSV map into `FFXIVPatchGenerator\bin\Release\rsv.json`, embed it into the single `FFXIVKoreanPatch.exe`, and extract it beside the embedded generator at runtime.
- Set `FFXIV_RSV_MAP_PATH` before running `Scripts\build-release.ps1` or `Scripts\build-test.ps1` to embed a local RSV map instead of downloading the default URL.
- RSV replacement runs after a final EXD string is selected from the Korean source row and before the row is serialized.
- Rows or columns intentionally preserved in the base/global language are not RSV-replaced. Those keep the base client RSV token so the base client/server path can resolve it normally.
- `InstanceContentTextData#45500` is a known upstream extraction exception: its auto-translate bracket decoration arrives as ASCII `7`/`8`, and its SeString newline macro arrives as `\n`. The direct EXD replacement path cannot preserve the native RSV consumer's formatting behavior. For the two exact Korean keys and exact extracted value, the resolver writes literal Korean phrases, explicitly tinted U+E040/U+E041 bracket glyphs, and an actual SeString newline macro. Any changed key or value is left untouched or rejected instead of guessed.

## Language IDs

EXD language IDs and RSV language IDs are different. The resolver uses RSV IDs:

- `ja`: `0`
- `en`: `1`
- `de`: `2`
- `fr`: `3`
- `chs`: `4`
- `cht`: `5`
- `ko`: `6`

This is why a Korean source token such as `_rsv_..._-1_6_...` must not be interpreted with the EXD `ko=7` language ID.

## Verification notes

2026-06-08 smoke verification with `https://github.com/Bing-su/my-ffxiv-toolkit/blob/main/rsv.json`:

- `Action` single-sheet build: 118 RSV tokens resolved, 0 unresolved, residual `rsvRows=0`, `rsvStrings=0`.
- Full text-only JA build: 193 RSV tokens resolved, 0 unresolved, residual `rsvRows=0`, `rsvStrings=0`.
- Resolved sheets in the full run: `action`, `instancecontenttextdata`, `npcyell`, `status`.

Use `patch-diagnostics.tsv` for sheet-level checks and diagnostic CSV notes such as `rsv-resolved=1` or `rsv-unresolved=1`.

2026-08-13 colored-bracket correction:

- Direct U+E040/U+E041 characters used the dialogue's default text color and appeared black in live testing.
- Replacing the phrases with valid `MacroCode.Fixed` Completion payloads was also rejected by live testing: the `InstanceContentTextData` speech path displayed `ケフカ:` followed only by the remaining whitespace/newline. A generated-archive byte check had proved only that the payload bytes were present, not that this UI path evaluated them.
- The replacement representation avoids `Fixed`: green `Color` push + U+E040 + color pop, literal Korean phrase, red `Color` push + U+E041 + color pop, and `02 10 01 03` for the line break. The selected bracket tints are RGBA `#7FBF5FFF` and `#C1584FFF`, matching the game's green-open/red-close convention shown in the reference client capture. Publish remains blocked until this exact representation is confirmed in the live encounter.

## Follow-up

The first implementation consumes an existing `rsv.json`; it does not yet extract fresh RSV values.

Initial extraction research:

- Triggevent parses ACT/OverlayPlugin line `262` as RSV data. The parser fields are `locale`, `number`, `rsvKey`, and `rsvValue`; blank values are ignored.
- Observed line shape: `262|timestamp|en|0000000A|_rsv_...|Inside Out|`.
- Triggevent then stores `RsvEvent(lang, key, value)` in a persistent language-specific library.
- Its persistent store writes one properties file per language under a local `rsv` directory.
- The changelog notes that after zoning in or replaying a log, `_rsv` actions/buffs for that fight can show their true names. This implies RSV values are best collected from runtime/log events for the relevant content, not assumed to be fully recoverable from static EXD alone.

Primary references:

- `Line262Parser`: https://github.com/xpdota/event-trigger/blob/master/xivsupport/src/main/java/gg/xp/xivsupport/events/actlines/parsers/Line262Parser.java
- `RsvProcessor`: https://github.com/xpdota/event-trigger/blob/master/xivsupport/src/main/java/gg/xp/xivsupport/rsv/RsvProcessor.java
- `PersistentRsvLibrary`: https://github.com/xpdota/event-trigger/blob/master/xivsupport/src/main/java/gg/xp/xivsupport/rsv/PersistentRsvLibrary.java
- `DefaultRsvLibrary.tryResolve`: https://github.com/xpdota/event-trigger/blob/master/xivdata/src/main/java/gg/xp/xivdata/data/rsv/DefaultRsvLibrary.java

Next implementation target:

- Add an importer that reads ACT/OverlayPlugin logs, extracts line `262` RSV entries, converts them to the flat `rsv.json` shape expected by the generator, and stores them under a versioned local cache such as `%LOCALAPPDATA%\FFXIVKoreanPatch\rsv\<client-version>\rsv.json`.
- Make the generator prefer the versioned local cache before falling back to exe-side `rsv.json`.
