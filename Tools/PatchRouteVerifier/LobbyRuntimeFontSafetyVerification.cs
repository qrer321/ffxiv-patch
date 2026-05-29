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
                            glyph.ImageIndex >= 24 ||
                            (IsStartupLobbyTextureLimitedFont(fontPath) && glyph.ImageIndex >= 12) ||
                            (IsObservedHdAnalyzerPage2UnsafeLobbyFont(fontPath) && glyph.ImageIndex >= 8))
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
                VerifyLobbyPatchedHangulMipConsistency(patchedHangulCells, ref failures);

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

            private static bool IsStartupLobbyTextureLimitedFont(string path)
            {
                string normalized = (path ?? string.Empty).Replace('\\', '/');
                return string.Equals(normalized, "common/font/AXIS_12_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/AXIS_14_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/AXIS_18_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/MiedingerMid_12_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/MiedingerMid_14_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/MiedingerMid_18_lobby.fdt", StringComparison.OrdinalIgnoreCase);
            }

            private static bool IsObservedHdAnalyzerPage2UnsafeLobbyFont(string path)
            {
                string normalized = (path ?? string.Empty).Replace('\\', '/');
                return string.Equals(normalized, "common/font/AXIS_12_lobby.fdt", StringComparison.OrdinalIgnoreCase);
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

            private void VerifyLobbyPatchedHangulMipConsistency(List<LobbyRuntimeGlyphCell> cells, ref int failures)
            {
                Dictionary<string, Texture> textureCache = new Dictionary<string, Texture>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> checkedCells = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
                {
                    if (failures >= MaxLobbyRuntimeSafetyFailures)
                    {
                        return;
                    }

                    LobbyRuntimeGlyphCell cell = cells[cellIndex];
                    string key = cell.TexturePath + "#" + cell.Channel.ToString() + "#" +
                                 cell.X.ToString() + "," + cell.Y.ToString() + "," +
                                 cell.Width.ToString() + "x" + cell.Height.ToString();
                    if (checkedCells.Contains(key))
                    {
                        continue;
                    }

                    checkedCells.Add(key);
                    Texture texture;
                    if (!textureCache.TryGetValue(cell.TexturePath, out texture))
                    {
                        try
                        {
                            texture = ReadFontTexture(_patchedFont, cell.TexturePath);
                            textureCache.Add(cell.TexturePath, texture);
                        }
                        catch (Exception ex)
                        {
                            failures = FailLobbyRuntimeSafetyOnce(
                                failures,
                                "{0} U+{1:X4} patched Hangul mip check cannot read {2}: {3}",
                                cell.FontPath,
                                cell.Codepoint,
                                cell.TexturePath,
                                ex.Message);
                            continue;
                        }
                    }

                    for (int level = 1; level < texture.MipmapCount; level++)
                    {
                        if (!IsTextureMipAvailable(texture, level))
                        {
                            failures = FailLobbyRuntimeSafetyOnce(
                                failures,
                                "{0} U+{1:X4} patched Hangul cell {2}:{3},{4} {5}x{6} has unavailable mip level {7}",
                                cell.FontPath,
                                cell.Codepoint,
                                cell.TexturePath,
                                cell.X,
                                cell.Y,
                                cell.Width,
                                cell.Height,
                                level);
                            continue;
                        }

                        LobbyMipDiff diff = MeasureLobbyPatchedHangulMipDiff(texture, cell, level);
                        if (diff.CheckedPixels == 0)
                        {
                            continue;
                        }

                        int allowedMismatches = Math.Max(2, diff.CheckedPixels / 8);
                        if ((diff.ExpectedVisible > 0 && diff.ActualVisible == 0) ||
                            diff.MismatchedPixels > allowedMismatches ||
                            diff.MaxDiff > 85)
                        {
                            failures = FailLobbyRuntimeSafetyOnce(
                                failures,
                                "{0} U+{1:X4} patched Hangul cell {2}:{3},{4} {5}x{6} mip {7} does not match base glyph downsample: checked={8}, expectedVisible={9}, actualVisible={10}, mismatched={11}, maxDiff={12}",
                                cell.FontPath,
                                cell.Codepoint,
                                cell.TexturePath,
                                cell.X,
                                cell.Y,
                                cell.Width,
                                cell.Height,
                                level,
                                diff.CheckedPixels,
                                diff.ExpectedVisible,
                                diff.ActualVisible,
                                diff.MismatchedPixels,
                                diff.MaxDiff);
                        }
                    }
                }
            }

            private static LobbyMipDiff MeasureLobbyPatchedHangulMipDiff(Texture texture, LobbyRuntimeGlyphCell cell, int level)
            {
                LobbyMipDiff diff = new LobbyMipDiff();
                int mipWidth = GetTextureMipWidth(texture, level);
                int mipHeight = GetTextureMipHeight(texture, level);
                int left = ClampInt(FloorDivPow2(cell.X, level), 0, mipWidth);
                int top = ClampInt(FloorDivPow2(cell.Y, level), 0, mipHeight);
                int right = ClampInt(CeilDivPow2(cell.X + cell.Width, level), 0, mipWidth);
                int bottom = ClampInt(CeilDivPow2(cell.Y + cell.Height, level), 0, mipHeight);
                int blockSize = 1 << Math.Min(level, 30);
                for (int y = top; y < bottom; y++)
                {
                    int baseY0 = Math.Max(0, y * blockSize - cell.Y);
                    int baseY1 = Math.Min(cell.Height, (y + 1) * blockSize - cell.Y);
                    if (baseY0 >= baseY1)
                    {
                        continue;
                    }

                    for (int x = left; x < right; x++)
                    {
                        int baseX0 = Math.Max(0, x * blockSize - cell.X);
                        int baseX1 = Math.Min(cell.Width, (x + 1) * blockSize - cell.X);
                        if (baseX0 >= baseX1)
                        {
                            continue;
                        }

                        byte expected = DownsampleLobbyBaseGlyphAlpha(texture, cell, baseX0, baseX1, baseY0, baseY1);
                        byte actual = ReadFontTextureMipAlphaOrZero(texture, x, y, cell.Channel, level);
                        if (expected > 0)
                        {
                            diff.ExpectedVisible++;
                        }

                        if (actual > 0)
                        {
                            diff.ActualVisible++;
                        }

                        int valueDiff = Math.Abs((int)expected - actual);
                        if (valueDiff > 34)
                        {
                            diff.MismatchedPixels++;
                        }

                        if (valueDiff > diff.MaxDiff)
                        {
                            diff.MaxDiff = valueDiff;
                        }

                        diff.CheckedPixels++;
                    }
                }

                return diff;
            }

            private static byte DownsampleLobbyBaseGlyphAlpha(
                Texture texture,
                LobbyRuntimeGlyphCell cell,
                int baseX0,
                int baseX1,
                int baseY0,
                int baseY1)
            {
                byte maxAlpha = 0;
                for (int y = baseY0; y < baseY1; y++)
                {
                    for (int x = baseX0; x < baseX1; x++)
                    {
                        byte alpha = ReadFontTextureAlphaOrZero(
                            texture,
                            0,
                            cell.X + x,
                            cell.Y + y,
                            cell.Channel);
                        if (alpha > maxAlpha)
                        {
                            maxAlpha = alpha;
                        }
                    }
                }

                return maxAlpha;
            }

            private static int ClampInt(int value, int min, int max)
            {
                if (value < min)
                {
                    return min;
                }

                if (value > max)
                {
                    return max;
                }

                return value;
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

            private struct LobbyMipDiff
            {
                public int CheckedPixels;
                public int ExpectedVisible;
                public int ActualVisible;
                public int MismatchedPixels;
                public int MaxDiff;
            }
        }
    }
}
