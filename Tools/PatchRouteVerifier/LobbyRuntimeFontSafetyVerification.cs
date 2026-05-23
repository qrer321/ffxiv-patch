using System;
using System.Collections.Generic;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private const int MaxLobbyRuntimeSafetyFailures = 80;

            private void VerifyLobbyRuntimeFontSafety()
            {
                Console.WriteLine("[FDT] Lobby runtime font safety");

                List<LobbyRuntimeGlyphCell> patchedHangulCells = new List<LobbyRuntimeGlyphCell>();
                int checkedFonts = 0;
                int checkedGlyphs = 0;
                int failures = 0;

                for (int fontIndex = 0; fontIndex < LobbyPhraseFontPaths.Length; fontIndex++)
                {
                    string fontPath = LobbyPhraseFontPaths[fontIndex];
                    byte[] patchedFdt;
                    byte[] cleanFdt;
                    try
                    {
                        patchedFdt = _patchedFont.ReadFile(fontPath);
                        cleanFdt = _cleanFont.ReadFile(fontPath);
                    }
                    catch (Exception ex)
                    {
                        failures = FailLobbyRuntimeSafetyOnce(failures, "{0} could not be read: {1}", fontPath, ex.Message);
                        continue;
                    }

                    int fontTableOffset;
                    uint glyphCount;
                    int glyphStart;
                    if (!TryGetFdtGlyphTable(patchedFdt, out fontTableOffset, out glyphCount, out glyphStart))
                    {
                        failures = FailLobbyRuntimeSafetyOnce(failures, "{0} patched glyph table is invalid", fontPath);
                        continue;
                    }

                    if (!VerifyLobbyFdtKerningOffset(fontPath, patchedFdt, ref failures))
                    {
                        continue;
                    }

                    VerifyLobbyUtf8OnlyKerningEntries(fontPath, patchedFdt, cleanFdt, ref failures);

                    Dictionary<uint, FdtGlyphEntry> cleanGlyphs = ReadGlyphEntriesByUtf8(cleanFdt);
                    checkedFonts++;
                    for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                    {
                        int glyphOffset = glyphStart + glyphIndex * FdtGlyphEntrySize;
                        uint codepoint;
                        if (!TryDecodeFdtUtf8Value(ReadU32LE(patchedFdt, glyphOffset), out codepoint) ||
                            !IsHangulCodepoint(codepoint))
                        {
                            continue;
                        }

                        FdtGlyphEntry glyph = ReadGlyphEntry(patchedFdt, glyphOffset);
                        if (glyph.Width == 0 || glyph.Height == 0)
                        {
                            continue;
                        }

                        checkedGlyphs++;
                        FdtGlyphEntry cleanGlyph;
                        if (cleanGlyphs.TryGetValue(codepoint, out cleanGlyph) &&
                            SameLobbyRuntimeCell(glyph, cleanGlyph))
                        {
                            continue;
                        }

                        string texturePath = ResolveFontTexturePath(fontPath, glyph.ImageIndex);
                        if (!IsLobbyTexturePath(texturePath) ||
                            string.Equals(texturePath, "common/font/font_lobby7.tex", StringComparison.OrdinalIgnoreCase) ||
                            glyph.ImageIndex >= 24)
                        {
                            failures = FailLobbyRuntimeSafetyOnce(
                                failures,
                                "{0} U+{1:X4} patched Hangul glyph uses unsafe lobby texture route image_index={2}, texture={3}",
                                fontPath,
                                codepoint,
                                glyph.ImageIndex,
                                texturePath ?? "n/a");
                            continue;
                        }

                        patchedHangulCells.Add(new LobbyRuntimeGlyphCell
                        {
                            FontPath = fontPath,
                            Codepoint = codepoint,
                            TexturePath = texturePath,
                            Channel = glyph.ImageIndex % 4,
                            X = glyph.X,
                            Y = glyph.Y,
                            Width = glyph.Width,
                            Height = glyph.Height
                        });
                    }
                }

                VerifyLobbyPatchedHangulCellOverlap(patchedHangulCells, ref failures);

                if (failures >= MaxLobbyRuntimeSafetyFailures)
                {
                    Warn("lobby runtime font safety check stopped after {0} failures", MaxLobbyRuntimeSafetyFailures);
                }

                if (failures == 0)
                {
                    Pass(
                        "lobby runtime font safety passed: fonts={0}, hangul_glyphs={1}, patched_hangul_cells={2}",
                        checkedFonts,
                        checkedGlyphs,
                        patchedHangulCells.Count);
                }
            }

            private void VerifyLobbyUtf8OnlyKerningEntries(string fontPath, byte[] patchedFdt, byte[] cleanFdt, ref int failures)
            {
                Dictionary<string, byte[]> cleanEntries = ReadKerningEntriesByKey(cleanFdt);
                int kerningStart;
                uint kerningCount;
                if (!TryGetKerningTable(patchedFdt, out kerningStart, out kerningCount))
                {
                    return;
                }

                for (int i = 0; i < kerningCount; i++)
                {
                    int offset = kerningStart + i * FdtKerningEntrySize;
                    uint leftValue = Endian.ReadUInt32LE(patchedFdt, offset);
                    uint rightValue = Endian.ReadUInt32LE(patchedFdt, offset + 4);
                    string key = leftValue.ToString("X8") + ":" + rightValue.ToString("X8");
                    if (cleanEntries.ContainsKey(key))
                    {
                        continue;
                    }

                    uint leftCodepoint;
                    uint rightCodepoint;
                    if (!TryDecodeFdtUtf8Value(leftValue, out leftCodepoint) ||
                        !TryDecodeFdtUtf8Value(rightValue, out rightCodepoint) ||
                        (!IsHangulCodepoint(leftCodepoint) && !IsHangulCodepoint(rightCodepoint)))
                    {
                        continue;
                    }

                    ushort leftShiftJis = Endian.ReadUInt16LE(patchedFdt, offset + 8);
                    ushort rightShiftJis = Endian.ReadUInt16LE(patchedFdt, offset + 10);
                    if (leftShiftJis != 0 && rightShiftJis != 0)
                    {
                        continue;
                    }

                    failures = FailLobbyRuntimeSafetyOnce(
                        failures,
                        "{0} synthetic kerning U+{1:X4}:U+{2:X4} has Shift-JIS fallback {3:X4}:{4:X4}; lobby runtime fonts must not add UTF-8-only Hangul kerning entries",
                        fontPath,
                        leftCodepoint,
                        rightCodepoint,
                        leftShiftJis,
                        rightShiftJis);
                }
            }

            private static Dictionary<string, byte[]> ReadKerningEntriesByKey(byte[] fdt)
            {
                Dictionary<string, byte[]> entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                int kerningStart;
                uint kerningCount;
                if (!TryGetKerningTable(fdt, out kerningStart, out kerningCount))
                {
                    return entries;
                }

                for (int i = 0; i < kerningCount; i++)
                {
                    int offset = kerningStart + i * FdtKerningEntrySize;
                    uint leftValue = Endian.ReadUInt32LE(fdt, offset);
                    uint rightValue = Endian.ReadUInt32LE(fdt, offset + 4);
                    byte[] entry = new byte[FdtKerningEntrySize];
                    Buffer.BlockCopy(fdt, offset, entry, 0, FdtKerningEntrySize);
                    entries[leftValue.ToString("X8") + ":" + rightValue.ToString("X8")] = entry;
                }

                return entries;
            }

            private bool VerifyLobbyFdtKerningOffset(string fontPath, byte[] fdt, ref int failures)
            {
                if (fdt == null || fdt.Length < FdtHeaderSize)
                {
                    failures = FailLobbyRuntimeSafetyOnce(failures, "{0} FDT is too short", fontPath);
                    return false;
                }

                uint kerningHeaderOffset = Endian.ReadUInt32LE(fdt, 0x0C);
                if (kerningHeaderOffset == 0)
                {
                    return true;
                }

                int kerningStart;
                uint kerningCount;
                if (!TryGetKerningTable(fdt, out kerningStart, out kerningCount))
                {
                    failures = FailLobbyRuntimeSafetyOnce(
                        failures,
                        "{0} kerning table offset 0x{1:X8} is invalid after glyph table rewrite",
                        fontPath,
                        kerningHeaderOffset);
                    return false;
                }

                return true;
            }

            private void VerifyLobbyPatchedHangulCellOverlap(List<LobbyRuntimeGlyphCell> cells, ref int failures)
            {
                Dictionary<string, List<LobbyRuntimeGlyphCell>> byTextureChannel =
                    new Dictionary<string, List<LobbyRuntimeGlyphCell>>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < cells.Count; i++)
                {
                    LobbyRuntimeGlyphCell cell = cells[i];
                    string key = cell.TexturePath + "#" + cell.Channel.ToString();
                    List<LobbyRuntimeGlyphCell> existing;
                    if (!byTextureChannel.TryGetValue(key, out existing))
                    {
                        existing = new List<LobbyRuntimeGlyphCell>();
                        byTextureChannel.Add(key, existing);
                    }

                    for (int otherIndex = 0; otherIndex < existing.Count; otherIndex++)
                    {
                        LobbyRuntimeGlyphCell other = existing[otherIndex];
                        if (!RectanglesOverlap(
                                cell.X,
                                cell.Y,
                                cell.Width,
                                cell.Height,
                                other.X,
                                other.Y,
                                other.Width,
                                other.Height) ||
                            SameLobbyRuntimeSharedCell(cell, other))
                        {
                            continue;
                        }

                        failures = FailLobbyRuntimeSafetyOnce(
                            failures,
                            "{0} U+{1:X4} patched Hangul cell {2}:{3},{4} {5}x{6} overlaps {7} U+{8:X4} cell {9},{10} {11}x{12}; patched lobby glyph cells must be isolated for UI-resolution reload safety",
                            cell.FontPath,
                            cell.Codepoint,
                            cell.TexturePath,
                            cell.X,
                            cell.Y,
                            cell.Width,
                            cell.Height,
                            other.FontPath,
                            other.Codepoint,
                            other.X,
                            other.Y,
                            other.Width,
                            other.Height);
                    }

                    existing.Add(cell);
                }
            }

            private static bool SameLobbyRuntimeCell(FdtGlyphEntry left, FdtGlyphEntry right)
            {
                return left.ImageIndex == right.ImageIndex &&
                       left.X == right.X &&
                       left.Y == right.Y &&
                       left.Width == right.Width &&
                       left.Height == right.Height;
            }

            private static bool SameLobbyRuntimeSharedCell(LobbyRuntimeGlyphCell left, LobbyRuntimeGlyphCell right)
            {
                return left.Codepoint == right.Codepoint &&
                       string.Equals(left.TexturePath, right.TexturePath, StringComparison.OrdinalIgnoreCase) &&
                       left.Channel == right.Channel &&
                       left.X == right.X &&
                       left.Y == right.Y &&
                       left.Width == right.Width &&
                       left.Height == right.Height;
            }

            private int FailLobbyRuntimeSafetyOnce(int failures, string format, params object[] args)
            {
                if (failures < MaxLobbyRuntimeSafetyFailures)
                {
                    Fail(format, args);
                }

                return failures + 1;
            }

            private struct LobbyRuntimeGlyphCell
            {
                public string FontPath;
                public uint Codepoint;
                public string TexturePath;
                public int Channel;
                public int X;
                public int Y;
                public int Width;
                public int Height;
            }
        }
    }
}
