# RSV token support

## Current behavior

- The generator accepts `--rsv-map <file>` for a flat JSON object mapping RSV keys to text values.
- If `--rsv-map` is not supplied, the generator auto-detects `rsv.json` beside `FFXIVPatchGenerator.exe`, then `rsv.json` in the current working directory.
- The RSV map is not bundled in this repository. Update the external `rsv.json` when new content adds RSV keys.
- Release builds download the current RSV map into `FFXIVPatchGenerator\bin\Release\rsv.json`, embed it into the single `FFXIVKoreanPatch.exe`, and extract it beside the embedded generator at runtime.
- Set `FFXIV_RSV_MAP_PATH` before running `Scripts\build-release.ps1` or `Scripts\build-test.ps1` to embed a local RSV map instead of downloading the default URL.
- RSV replacement runs after a final EXD string is selected from the Korean source row and before the row is serialized.
- Rows or columns intentionally preserved in the base/global language are not RSV-replaced. Those keep the base client RSV token so the base client/server path can resolve it normally.

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
