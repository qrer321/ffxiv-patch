using System;
using System.Collections.Generic;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private const int MaxFdtTableInvariantFailures = 80;

            private void VerifyFdtTableInvariants()
            {
                Console.WriteLine("[FDT] Font table invariants");

                string[] fontPaths = CreateFdtTableInvariantFontPaths();
                int checkedFonts = 0;
                int checkedGlyphs = 0;
                int checkedKerning = 0;
                int failures = 0;

                for (int fontIndex = 0; fontIndex < fontPaths.Length; fontIndex++)
                {
                    if (failures >= MaxFdtTableInvariantFailures)
                    {
                        break;
                    }

                    string fontPath = fontPaths[fontIndex];
                    byte[] fdt;
                    byte[] cleanFdt = null;
                    try
                    {
                        fdt = _patchedFont.ReadFile(fontPath);
                    }
                    catch (Exception ex)
                    {
                        failures = FailFdtTableInvariantOnce(failures, "{0} could not be read: {1}", fontPath, ex.Message);
                        continue;
                    }

                    try
                    {
                        cleanFdt = _cleanFont.ReadFile(fontPath);
                    }
                    catch
                    {
                        cleanFdt = null;
                    }

                    int glyphFailuresBefore = failures;
                    checkedGlyphs += VerifyFdtGlyphTableInvariant(fontPath, fdt, cleanFdt, ref failures);
                    checkedKerning += VerifyFdtKerningTableInvariant(fontPath, fdt, cleanFdt, ref failures);
                    if (failures == glyphFailuresBefore)
                    {
                        checkedFonts++;
                    }
                }

                if (failures >= MaxFdtTableInvariantFailures)
                {
                    Warn("FDT table invariant check stopped after {0} failures", MaxFdtTableInvariantFailures);
                }

                if (failures == 0)
                {
                    Pass(
                        "FDT table invariants passed: fonts={0}, glyphs={1}, kerning={2}",
                        checkedFonts,
                        checkedGlyphs,
                        checkedKerning);
                }
            }

            private int VerifyFdtGlyphTableInvariant(string fontPath, byte[] fdt, byte[] cleanFdt, ref int failures)
            {
                int fontTableOffset;
                uint glyphCount;
                int glyphStart;
                if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
                {
                    if (cleanFdt == null || TryGetFdtGlyphTable(cleanFdt, out fontTableOffset, out glyphCount, out glyphStart))
                    {
                        failures = FailFdtTableInvariantOnce(failures, "{0} glyph table is invalid", fontPath);
                    }

                    return 0;
                }

                uint previousValue = 0;
                bool hasPrevious = false;
                Dictionary<uint, int> cleanKeyCounts = CountFdtGlyphKeys(cleanFdt);
                Dictionary<uint, int> patchedKeyCounts = new Dictionary<uint, int>();
                int checkedGlyphs = 0;

                for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                {
                    int offset = glyphStart + glyphIndex * FdtGlyphEntrySize;
                    uint value = Endian.ReadUInt32LE(fdt, offset);
                    uint codepoint;
                    if (!TryDecodeFdtUtf8Value(value, out codepoint))
                    {
                        failures = FailFdtTableInvariantOnce(
                            failures,
                            "{0} glyph[{1}] has invalid UTF-8 key 0x{2:X8}",
                            fontPath,
                            glyphIndex,
                            value);
                        continue;
                    }

                    if (hasPrevious && value < previousValue)
                    {
                        failures = FailFdtTableInvariantOnce(
                            failures,
                            "{0} glyph table is not sorted at glyph[{1}] U+{2:X4} key=0x{3:X8} after key=0x{4:X8}",
                            fontPath,
                            glyphIndex,
                            codepoint,
                            value,
                            previousValue);
                    }

                    int patchedCount;
                    patchedKeyCounts.TryGetValue(value, out patchedCount);
                    patchedCount++;
                    patchedKeyCounts[value] = patchedCount;
                    int cleanCount;
                    cleanKeyCounts.TryGetValue(value, out cleanCount);
                    if (patchedCount > Math.Max(1, cleanCount))
                    {
                        failures = FailFdtTableInvariantOnce(
                            failures,
                            "{0} glyph table introduced duplicate UTF-8 key 0x{1:X8} (U+{2:X4}); clean_count={3}, patched_count={4}",
                            fontPath,
                            value,
                            codepoint,
                            cleanCount,
                            patchedCount);
                    }

                    FdtGlyphEntry glyph = ReadGlyphEntry(fdt, offset);
                    if (glyph.Width > 0 && glyph.Height > 0)
                    {
                        ushort expectedShiftJis;
                        if (TryEncodeShiftJisValue(codepoint, out expectedShiftJis) &&
                            glyph.ShiftJisValue != expectedShiftJis &&
                            !CleanGlyphHasSameShiftJisValue(cleanFdt, codepoint, glyph.ShiftJisValue))
                        {
                            failures = FailFdtTableInvariantOnce(
                                failures,
                                "{0} U+{1:X4} Shift-JIS fallback mismatch: expected=0x{2:X4}, actual=0x{3:X4}",
                                fontPath,
                                codepoint,
                                expectedShiftJis,
                                glyph.ShiftJisValue);
                        }
                    }

                    previousValue = value;
                    hasPrevious = true;
                    checkedGlyphs++;
                }

                return checkedGlyphs;
            }

            private int VerifyFdtKerningTableInvariant(string fontPath, byte[] fdt, byte[] cleanFdt, ref int failures)
            {
                int kerningStart;
                uint kerningCount;
                if (!TryGetKerningTable(fdt, out kerningStart, out kerningCount))
                {
                    return 0;
                }

                uint previousLeft = 0;
                uint previousRight = 0;
                bool hasPrevious = false;
                Dictionary<string, int> cleanPairCounts = CountFdtKerningKeys(cleanFdt);
                Dictionary<string, int> patchedPairCounts = new Dictionary<string, int>(StringComparer.Ordinal);

                for (int kerningIndex = 0; kerningIndex < kerningCount; kerningIndex++)
                {
                    int offset = kerningStart + kerningIndex * FdtKerningEntrySize;
                    uint left = Endian.ReadUInt32LE(fdt, offset);
                    uint right = Endian.ReadUInt32LE(fdt, offset + 4);
                    string key = left.ToString("X8") + ":" + right.ToString("X8");

                    if (hasPrevious &&
                        (left < previousLeft || (left == previousLeft && right < previousRight)))
                    {
                        failures = FailFdtTableInvariantOnce(
                            failures,
                            "{0} kerning table is not sorted at kerning[{1}] key={2} after {3:X8}:{4:X8}",
                            fontPath,
                            kerningIndex,
                            key,
                            previousLeft,
                            previousRight);
                    }

                    int patchedCount;
                    patchedPairCounts.TryGetValue(key, out patchedCount);
                    patchedCount++;
                    patchedPairCounts[key] = patchedCount;
                    int cleanCount;
                    cleanPairCounts.TryGetValue(key, out cleanCount);
                    if (patchedCount > Math.Max(1, cleanCount))
                    {
                        failures = FailFdtTableInvariantOnce(
                            failures,
                            "{0} kerning table introduced duplicate key {1}; clean_count={2}, patched_count={3}",
                            fontPath,
                            key,
                            cleanCount,
                            patchedCount);
                    }

                    previousLeft = left;
                    previousRight = right;
                    hasPrevious = true;
                }

                return checked((int)kerningCount);
            }

            private static Dictionary<uint, int> CountFdtGlyphKeys(byte[] fdt)
            {
                Dictionary<uint, int> counts = new Dictionary<uint, int>();
                if (fdt == null)
                {
                    return counts;
                }

                int fontTableOffset;
                uint glyphCount;
                int glyphStart;
                if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
                {
                    return counts;
                }

                for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                {
                    int offset = glyphStart + glyphIndex * FdtGlyphEntrySize;
                    uint value = Endian.ReadUInt32LE(fdt, offset);
                    int count;
                    counts.TryGetValue(value, out count);
                    counts[value] = count + 1;
                }

                return counts;
            }

            private static Dictionary<string, int> CountFdtKerningKeys(byte[] fdt)
            {
                Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
                if (fdt == null)
                {
                    return counts;
                }

                int kerningStart;
                uint kerningCount;
                if (!TryGetKerningTable(fdt, out kerningStart, out kerningCount))
                {
                    return counts;
                }

                for (int kerningIndex = 0; kerningIndex < kerningCount; kerningIndex++)
                {
                    int offset = kerningStart + kerningIndex * FdtKerningEntrySize;
                    string key = Endian.ReadUInt32LE(fdt, offset).ToString("X8") + ":" +
                                 Endian.ReadUInt32LE(fdt, offset + 4).ToString("X8");
                    int count;
                    counts.TryGetValue(key, out count);
                    counts[key] = count + 1;
                }

                return counts;
            }

            private static bool CleanGlyphHasSameShiftJisValue(byte[] cleanFdt, uint codepoint, ushort shiftJisValue)
            {
                FdtGlyphEntry cleanGlyph;
                return cleanFdt != null &&
                       TryFindGlyph(cleanFdt, codepoint, out cleanGlyph) &&
                       cleanGlyph.ShiftJisValue == shiftJisValue;
            }

            private static string[] CreateFdtTableInvariantFontPaths()
            {
                HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AddFdtTableInvariantFontPaths(paths, LobbyPhraseFontPaths);
                AddFdtTableInvariantFontPaths(paths, NumericGlyphSameFontChecks);
                AddFdtTableInvariantFontPaths(paths, PartyListSelfMarkerSameFontChecks);
                AddFdtTableInvariantFontPaths(paths, ProtectedHangulFonts);
                AddFdtTableInvariantFontPaths(paths, new string[]
                {
                    "common/font/KrnAXIS_120.fdt",
                    "common/font/KrnAXIS_140.fdt",
                    "common/font/KrnAXIS_180.fdt",
                    "common/font/KrnAXIS_360.fdt"
                });

                string[] values = new string[paths.Count];
                paths.CopyTo(values);
                Array.Sort(values, StringComparer.OrdinalIgnoreCase);
                return values;
            }

            private static void AddFdtTableInvariantFontPaths(HashSet<string> paths, string[] values)
            {
                if (values == null)
                {
                    return;
                }

                for (int i = 0; i < values.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(values[i]))
                    {
                        paths.Add(values[i]);
                    }
                }
            }

            private int FailFdtTableInvariantOnce(int failures, string format, params object[] args)
            {
                if (failures < MaxFdtTableInvariantFailures)
                {
                    Fail(format, args);
                }

                return failures + 1;
            }
        }
    }
}
