# FontPatchAssets

Place the original font patch package files here when building local test or release artifacts:

- `TTMPD.mpd`
- `TTMPL.mpl`

These files are copied next to `FFXIVPatchGenerator.exe` during build and are used first for font patch generation. They are intentionally ignored by Git because they are generated/mod package data, not source code.
