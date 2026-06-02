# Base Language Name Policy

## Purpose

Keep boss/NPC name text and action/skill name text in the selected base client
language while the rest of the Korean patch continues to use Korean source text.

This is currently a built-in generator policy only. Do not expose it as a UI
option until the option scope and separate verifier are added.

## Current Scope

The default policy preserves string column offset `0` from the target global
client language for these sheets:

- `BNpcName`
- `Action`
- `BuddyAction`
- `CraftAction`
- `EventAction`
- `GeneralAction`
- `PetAction`

`MountAction` and `PvPAction` currently have no string columns in the checked
2026.05.25 data set, so they are not part of the active verified scope.

For Japanese-client output this means the visible name column stays Japanese.
For English-client output the same policy should keep the English name column,
but English output must be verified separately before release.

## Verification Notes

Use local restore-baseline clean indexes, not the currently patched game folder.

Checked on 2026.05.25 `ja` data:

- `Action`: column offset `0` routes to `keep-global` for all diagnostic rows.
- `BNpcName`: column offset `0` routes to `keep-global`; column offset `4`
  still routes through normal replacement.
- `BuddyAction`, `CraftAction`, `EventAction`, `GeneralAction`, and `PetAction`:
  column offset `0` routes to `keep-global`.

This policy is text-routing only. It must not touch font, lobby font atlas,
ULD, or UI texture patch paths.
