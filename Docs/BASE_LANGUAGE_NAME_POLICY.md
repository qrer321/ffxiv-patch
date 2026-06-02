# Base Language Name Policy

## Purpose

Allow selected name columns to remain in the base global client language while
the rest of the text patch continues to use Korean source text.

This policy is text-routing only. It must not touch font, lobby font atlas, ULD,
or UI texture patch paths.

## UI Options

The patch UI exposes three independent checkboxes. All default to off.

- `BNpcName` original language: preserves the battle NPC name column from the
  selected base client language.
- Action name original language: preserves action/skill name columns from the
  selected base client language.
- Common phrase original language: preserves auto-translate/common phrase text
  from the selected base client language.

The local UI values are saved in:

`%LOCALAPPDATA%\FFXIVKoreanPatch\patch-options.txt`

## Current Scope

When enabled, the policy preserves string column offset `0` from the target
global client language for these groups:

- `bnpcname`: `BNpcName`
- `actions`: `Action`, `BuddyAction`, `CraftAction`, `EventAction`,
  `GeneralAction`, `PetAction`
- `commonphrases`: `Completion`

`BNpcName` and action groups preserve column offset `0`. `Completion` preserves
column offsets `0`, `4`, and `8`; preserving only column `0` is not enough for
the common phrase dictionary because its visible text spans multiple string
columns.

`MountAction` and `PvPAction` currently have no string columns in the checked
2026.05.25 data set, so they are not part of the active verified scope.

`ENpcResident` is intentionally not included in the original-language options.
Resident/NPC UI text should continue through the normal Korean patch route.

For Japanese-client output this means selected columns stay Japanese. For
English-client output the same policy should keep English, but English output
must be verified separately before release.

## Generator Flags

- `--preserve-base-bnpc-names`
- `--preserve-base-action-names`
- `--preserve-base-common-phrases`
- `--preserve-base-language-groups <csv>`

Legacy `--preserve-base-language-names` maps to `bnpcname` and `actions` only.
It is kept only for compatibility and does not enable common phrases.

## Verification Notes

Use local restore-baseline clean indexes, not the currently patched game folder.

Checked on 2026.05.25 `ja` data:

- Default/off: `BNpcName`, action sheets, `Completion`, and `ENpcResident` use
  normal Korean replacement routing where Korean rows exist.
- `--preserve-base-bnpc-names`: `BNpcName` column offset `0` routes to
  `keep-global`; action sheets and `ENpcResident` remain on normal routing.
- `--preserve-base-action-names`: action sheet column offset `0` routes to
  `keep-global`; `BNpcName` and `ENpcResident` remain on normal routing.
- `--preserve-base-common-phrases`: `Completion` column offsets `0`, `4`, and
  `8` route to `keep-global`; `BNpcName`, action sheets, and `ENpcResident`
  remain on normal routing.
