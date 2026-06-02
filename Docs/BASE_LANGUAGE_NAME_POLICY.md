# Base Language Name Policy

## Purpose

Allow selected name columns to remain in the base global client language while
the rest of the text patch continues to use Korean source text.

This policy is text-routing only. It must not touch font, lobby font atlas, ULD,
or UI texture patch paths.

## UI Options

The patch UI exposes two independent checkboxes. Both default to off.

- `BNpcName` original language: preserves the battle NPC name column from the
  selected base client language.
- Action name original language: preserves action/skill name columns from the
  selected base client language.

The local UI values are saved in:

`%LOCALAPPDATA%\FFXIVKoreanPatch\patch-options.txt`

## Current Scope

When enabled, the policy preserves string column offset `0` from the target
global client language for these groups:

- `bnpcname`: `BNpcName`
- `actions`: `Action`, `BuddyAction`, `CraftAction`, `EventAction`,
  `GeneralAction`, `PetAction`

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
- `--preserve-base-language-groups <csv>`

Legacy `--preserve-base-language-names` maps to both active groups. It is kept
only for compatibility.

## Verification Notes

Use local restore-baseline clean indexes, not the currently patched game folder.

Checked on 2026.05.25 `ja` data:

- Default/off: `BNpcName`, action sheets, and `ENpcResident` use normal Korean
  replacement routing where Korean rows exist.
- `--preserve-base-bnpc-names`: `BNpcName` column offset `0` routes to
  `keep-global`; action sheets and `ENpcResident` remain on normal routing.
- `--preserve-base-action-names`: action sheet column offset `0` routes to
  `keep-global`; `BNpcName` and `ENpcResident` remain on normal routing.
