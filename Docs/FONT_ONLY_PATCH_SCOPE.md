# Font-Only Patch Scope

## Purpose

`font-only` must mean a minimal patch for Korean text input/readability support. It must not behave like a reduced full translation patch.

The accepted scope is:

- Generate/apply only the `000000` common font package.
- Add or preserve Korean-readable font resources through the Korean-only `KrnAXIS_*` font routes.
- Keep global clean ASCII, numbers, symbols, and existing non-Korean glyph behavior stable.
- Do not generate/apply `0a0000` EXD text patches.
- Do not generate/apply `060000` UI texture/image patches.
- Do not localize lobby text, image-based title cards, maps, event images, or other full-patch UI resources.

## External Reference Findings

- `korean-patch/ffxiv-patch-ui` had a "chat only" path that downloaded/applied only `000000.win32.dat1` and `000000.win32.index`.
- `GpointChen/FFXIVChnTextPatch-GP` separates font replacement from text replacement: `ReplaFont` patches `resource/font` into `000000`, while `ReplaText` is a separate path.
- `Soreepeong/FFXIV-FontChanger` treats font support as font-resource work and exports an FDT/TEX TTMP font mod. It also documents the game font families separately: AXIS for standard text/chat, Jupiter for damage numbers, Meidinger for bar numbers, and TrumpGothic for window titles.
- Official Dalamud API docs expose two relevant font paths:
  - `IUiBuilder.DefaultFontHandle` is the default Dalamud font handle and is documented as supporting all game languages and icons: https://dalamud.dev/api/api14/Dalamud.Interface/Interfaces/IUiBuilder/
  - `IFontAtlas.NewGameFontHandle(GameFontStyle)` creates a font handle from the game's built-in fonts: https://dalamud.dev/api/api14/Dalamud.Interface.ManagedFontAtlas/Interfaces/IFontAtlas/
  - `GameFontFamilyAndSize` documents `Axis96/12/14/18/36` as AXIS fonts used for the whole game UI, while Jupiter damage fonts are separate: https://dalamud.dev/api/Dalamud.Interface.GameFonts/Enums/GameFontFamilyAndSize/

These references point to the same separation: font support belongs to `000000/common/font`; text/UI localization belongs elsewhere.

## Current Local Risk

Before this scope correction, our UI and generator mixed font-only with full-patch support:

- `--font-only` set `IncludeFont = true`, and `ShouldBuildUiTextureFix` still allowed `UiPatchGenerator`.
- The UI added both `fontPatchFiles` and `uiPatchFiles` whenever `includeFontPatch` was true.
- Font generation also included full-patch repair logic such as lobby Hangul allocation and ActionDetail high-scale repair.

The first two items are hard scope violations and must stay covered by verification. The third item is now gated: strict `font-only` skips lobby Hangul allocation, ActionDetail high-scale Hangul repair, supplemental lobby font generation, and start-screen system-settings kerning.

2026-05-17 correction: the AXIS/font1/font2/font3 expansion trial was rejected. The live third-party overlay break came from a full `--include-font` build that broadly edited shared game-font resources, not from a proven safe `font-only` scope. The failed direction kept TTMP AXIS Hangul cells and tried to restore clean ASCII/number/symbol cells across shared textures, but that still allowed shared atlas contamination and false-positive generated-output checks.

The accepted `font-only` scope is again strict:

- `common/font/KrnAXIS_120.fdt`
- `common/font/KrnAXIS_140.fdt`
- `common/font/KrnAXIS_180.fdt`
- `common/font/KrnAXIS_360.fdt`
- `common/font/font_krn_1.tex`

Do not add `AXIS_*`, `Jupiter_*`, `TrumpGothic_*`, `Miedinger*`, `Meidinger*`, `font1.tex`, `font2.tex`, `font3.tex`, lobby fonts, EXD text, or UI texture files to `font-only` unless a verifier first proves the exact target route and clean shared-atlas safety. Dalamud/plugin issues must be debugged as shared game-font route safety, not by broadening `font-only`.

## 2026-07-03 Widening: full in-game font treatment

The strict KrnAXIS-only scope left the live symptom unsolved: the base-language
client renders chat and UI text through the shared `AXIS_*` routes, so Korean
input still fell back to `=`. The precondition set above ("a verifier first
proves the exact target route and clean shared-atlas safety") is now met by the
full patch's font pipeline:

- `third-party-game-font-safety` (fonts=14) plus damage-number route/neighborhood,
  in-game clean ASCII, protected PUA, and TTMP Hangul preservation checks all
  pass on the full-patch output (2026-07-03 r9 20-check regression), and the
  live Dalamud/damage fly-text breaks were user-confirmed fixed on 2026-05-17.

`font-only` therefore now ships the **same in-game font treatment as the full
patch** — TTMP FDT/texture payloads plus the clean ASCII/number/symbol, damage
digit, and party PUA protections — for every non-lobby font file. Differences
from the full patch's `000000`:

- Lobby FDTs and `font_lobby*.tex` stay byte-identical to the clean client
  (font-only does not localize lobby text, and this keeps the HD/FHD lobby
  page budget out of scope).
- Korean-text-specific visual scaling stays off (ActionDetail high-scale,
  PvP profile downscale, lobby Hangul allocation, start-screen kerning,
  dialogue artifact fix): the affected labels remain base-language text in
  font-only, and skipping them keeps patched glyphs strictly TTMP-identical,
  so the source-preservation checks pass without intentional-change waivers.

`font-only-output-scope` enforces the new contract: any non-lobby font entry
may change, lobby entries must not, `AXIS_12/14/18/36` + `KrnAXIS_*` +
`font1.tex` must change, and Hangul smoke glyphs must be visible in both
`KrnAXIS_*` and `AXIS_12/14/18/36`.

Latest generated check for the corrected full-patch side: `.tmp\thirdparty-safe-ja-r7` passes `third-party-game-font-safety` (`fonts=14`, `glyphs=159889`) plus combat damage, in-game clean ASCII, TTMP Hangul texture-neighborhood, reported Hangul phrase, and ActionDetail checks. The user confirmed the live Dalamud/third-party Korean font break is fixed on 2026-05-17.

## Required Verification

Add and run a weak but explicit output-scope verifier before deeper glyph checks:

- `font-only-output-scope` must pass for a `--font-only` output.
- `font-only-output-scope` must also compare `000000.win32.index` against `orig.000000.win32.index` and fail if guarded shared/special font entries changed.
- The same check must smoke-test visible Hangul glyphs in the patched `KrnAXIS_*` routes.
- Required files:
  - `000000.win32.dat1`
  - `000000.win32.index`
  - `000000.win32.index2`
  - `orig.000000.win32.index`
  - `orig.000000.win32.index2`
- Forbidden files:
  - `0a0000.win32.dat1`
  - `0a0000.win32.index`
  - `0a0000.win32.index2`
  - `orig.0a0000.win32.index`
  - `orig.0a0000.win32.index2`
  - `patch-diagnostics.tsv`
  - `060000.win32.dat4`
  - `060000.win32.index`
  - `060000.win32.index2`
  - `orig.060000.win32.index`
  - `orig.060000.win32.index2`
  - `manifest.json`

After scope passes, run focused font checks such as:

- `ingame-clean-ascii-glyphs`
- `combat-flytext-damage-glyphs`
- `reported-ingame-hangul-phrases`
- `ingame-ttmp-texture-neighborhoods`
- `action-detail-scale-layouts`

Do not close live client issues from generated-output checks alone.

## Implementation Notes

- `BuildOptions.ShouldBuildUiTextureFix` must return false when `FontOnly` is true.
- UI `GetPatchFilesForSelection(false, true)` must select only `000000` font patch files.
- `FontPatchGenerator` must skip full-patch-only font repairs when `FontOnly` is true:
  - lobby Hangul allocation
  - supplemental lobby font generation
  - ActionDetail high-scale Hangul repair
  - start-screen system-settings kerning
- `font-only` must not perform shared `font1/font2/font3` clean ASCII/number/symbol repair. That belongs only to focused full-patch verifiers/fixes when a specific route requires it.
- Party-list PUA clean-shape repair still applies inside `KrnAXIS_*` because it is a font atlas protection, not text/UI localization.
- Damage fly text is user-confirmed fixed in the current full patch. Font-only rework must not modify damage-number routes or their shared texture neighborhoods as a side effect.
