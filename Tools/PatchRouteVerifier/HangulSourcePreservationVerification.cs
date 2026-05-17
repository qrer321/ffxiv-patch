using System;
using System.Collections.Generic;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private void VerifyHangulSourcePreservation()
            {
                Console.WriteLine("[FDT] Hangul TTMP source preservation");
                if (_ttmpFont == null)
                {
                    Warn("TTMP source preservation skipped; pass --font-pack-dir with TTMPD.mpd and TTMPL.mpl");
                    return;
                }

                Console.WriteLine("  TTMP font package: {0}", _ttmpFont.DirectoryPath);

                HashSet<string> fontPaths = CollectHangulSourcePreservationFontPaths();
                uint[] codepoints = CollectHangulSourcePreservationCodepoints();
                int compared = 0;
                int skippedBlank = 0;
                int skippedIntentional = 0;
                int skippedMissing = 0;
                int lobbyAxisAdvanceEntriesChecked = 0;
                int lobbyAxisAdvanceEntriesMatched = 0;
                HashSet<uint> actionDetailHighScaleCodepoints = CollectActionDetailHighScaleHangulCodepointSet();

                foreach (string fontPath in fontPaths)
                {
                    if (!_ttmpFont.ContainsPath(fontPath))
                    {
                        skippedMissing++;
                        continue;
                    }

                    byte[] sourceFdt;
                    byte[] targetFdt;
                    try
                    {
                        sourceFdt = _ttmpFont.ReadFile(fontPath);
                        targetFdt = _patchedFont.ReadFile(fontPath);
                    }
                    catch (Exception ex)
                    {
                        Fail("{0} source preservation read error: {1}", fontPath, ex.Message);
                        continue;
                    }

                    VerifyAllLobbyAxisHangulSourceEntries(
                        fontPath,
                        sourceFdt,
                        targetFdt,
                        ref lobbyAxisAdvanceEntriesChecked,
                        ref lobbyAxisAdvanceEntriesMatched);

                    for (int i = 0; i < codepoints.Length; i++)
                    {
                        uint codepoint = codepoints[i];
                        FdtGlyphEntry sourceGlyph;
                        FdtGlyphEntry targetGlyph;
                        if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                        {
                            continue;
                        }

                        if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                        {
                            Fail("{0} U+{1:X4} exists in TTMP source but is missing in patched font", fontPath, codepoint);
                            continue;
                        }

                        if (IsIntentionalHangulSourceChange(fontPath, codepoint, actionDetailHighScaleCodepoints))
                        {
                            skippedIntentional++;
                            continue;
                        }

                        GlyphCanvas source;
                        GlyphCanvas target;
                        try
                        {
                            source = RenderGlyph(_ttmpFont, fontPath, codepoint);
                            target = RenderGlyph(_patchedFont, fontPath, codepoint);
                        }
                        catch (Exception ex)
                        {
                            Fail("{0} U+{1:X4} source preservation render error: {2}", fontPath, codepoint, ex.Message);
                            continue;
                        }

                        if (source.VisiblePixels < 10)
                        {
                            skippedBlank++;
                            continue;
                        }

                        long score = Diff(source.Alpha, target.Alpha);
                        bool spacingMatch = GlyphSpacingMetricsMatch(sourceGlyph, targetGlyph);
                        if (score == 0 && spacingMatch && target.VisiblePixels >= 10)
                        {
                            compared++;
                            continue;
                        }

                        Fail(
                            "{0} U+{1:X4} changed from TTMP source: score={2}, visible={3}/{4}, patched={5}, source={6}",
                            fontPath,
                            codepoint,
                            score,
                            source.VisiblePixels,
                            target.VisiblePixels,
                            FormatGlyphSpacing(targetGlyph),
                            FormatGlyphSpacing(sourceGlyph));
                    }
                }

                if (compared == 0)
                {
                    Fail("No Hangul glyphs were compared against TTMP source");
                    return;
                }

                Pass(
                    "Hangul TTMP source glyphs preserved: compared={0}, lobby_axis_entries_matched={1}/{2}, skipped_blank={3}, skipped_intentional={4}, skipped_missing_fonts={5}",
                    compared,
                    lobbyAxisAdvanceEntriesMatched,
                    lobbyAxisAdvanceEntriesChecked,
                    skippedBlank,
                    skippedIntentional,
                    skippedMissing);
            }

            private static HashSet<string> CollectHangulSourcePreservationFontPaths()
            {
                HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AddValues(paths, DialoguePhraseFontPaths);
                AddValues(paths, SystemSettingsScaledFonts);
                for (int i = 0; i < Derived4kLobbyFontPairs.GetLength(0); i++)
                {
                    paths.Add(Derived4kLobbyFontPairs[i, 1]);
                }

                return paths;
            }

            private static uint[] CollectHangulSourcePreservationCodepoints()
            {
                HashSet<uint> codepoints = new HashSet<uint>();
                AddHangulCodepoints(codepoints, LobbyDiagnosticPhrases);
                AddHangulCodepoints(codepoints, DialogueDiagnosticPhrases);
                AddHangulCodepoints(codepoints, ReportedInGameHangulPhrases);
                AddHangulCodepoints(codepoints, SystemSettingsScaledPhrases);
                AddHangulCodepoints(codepoints, FourKLobbyPhrases);
                return ToSortedArray(codepoints);
            }

            private static void AddValues(HashSet<string> target, string[] values)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    target.Add(values[i]);
                }
            }

            private static void AddHangulCodepoints(HashSet<uint> target, string[] phrases)
            {
                for (int phraseIndex = 0; phraseIndex < phrases.Length; phraseIndex++)
                {
                    string phrase = phrases[phraseIndex] ?? string.Empty;
                    for (int i = 0; i < phrase.Length; i++)
                    {
                        uint codepoint = ReadCodepoint(phrase, ref i);
                        if (IsHangulCodepoint(codepoint))
                        {
                            target.Add(codepoint);
                        }
                    }
                }
            }

            private static uint[] ToSortedArray(HashSet<uint> values)
            {
                uint[] result = new uint[values.Count];
                values.CopyTo(result);
                Array.Sort(result);
                return result;
            }

            private static bool IsIntentionalHangulSourceChange(string fontPath, uint codepoint, HashSet<uint> actionDetailHighScaleCodepoints)
            {
                string normalized = fontPath.Replace('\\', '/');
                if (actionDetailHighScaleCodepoints != null &&
                    actionDetailHighScaleCodepoints.Contains(codepoint) &&
                    ActionDetailHighScaleHangulGlyphs.IsVisualScaleTargetFontPath(normalized))
                {
                    return true;
                }

                if (codepoint == 0xBCC0 &&
                    (string.Equals(normalized, "common/font/AXIS_18.fdt", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(normalized, "common/font/MiedingerMid_18.fdt", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(normalized, "common/font/TrumpGothic_184.fdt", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                return false;
            }

            private void VerifyAllLobbyAxisHangulSourceEntries(
                string fontPath,
                byte[] sourceFdt,
                byte[] targetFdt,
                ref int totalChecked,
                ref int totalMatched)
            {
                if (!IsLobbyAxisHangulAdvanceNormalizedFont(fontPath))
                {
                    return;
                }

                Dictionary<uint, FdtGlyphEntry> targetGlyphs = ReadHangulGlyphEntries(targetFdt);
                int fontTableOffset;
                uint glyphCount;
                int glyphStart;
                if (!TryGetFdtGlyphTable(sourceFdt, out fontTableOffset, out glyphCount, out glyphStart))
                {
                    Fail("{0} lobby AXIS Hangul advance scan could not read source glyph table", fontPath);
                    return;
                }

                int checkedEntries = 0;
                int matchedEntries = 0;
                for (int i = 0; i < glyphCount; i++)
                {
                    int offset = glyphStart + i * FdtGlyphEntrySize;
                    uint codepoint;
                    if (!TryDecodeFdtUtf8Value(Endian.ReadUInt32LE(sourceFdt, offset), out codepoint) ||
                        !IsHangulCodepoint(codepoint))
                    {
                        continue;
                    }

                    FdtGlyphEntry sourceGlyph = ReadGlyphEntry(sourceFdt, offset);
                    FdtGlyphEntry targetGlyph;
                    if (!targetGlyphs.TryGetValue(codepoint, out targetGlyph))
                    {
                        Fail("{0} U+{1:X4} lobby AXIS Hangul source entry is missing from patched font", fontPath, codepoint);
                        continue;
                    }

                    if (!LobbyAxisHangulAdvanceEntryMatchesExpected(sourceGlyph, targetGlyph))
                    {
                        Fail(
                            "{0} U+{1:X4} lobby AXIS Hangul source entry mismatch: target={2}, source={3}",
                            fontPath,
                            codepoint,
                            FormatGlyphEntryRoute(targetGlyph),
                            FormatGlyphEntryRoute(sourceGlyph));
                        continue;
                    }

                    checkedEntries++;
                    matchedEntries++;
                }

                if (checkedEntries == 0)
                {
                    Fail("{0} lobby AXIS Hangul advance scan found no source Hangul glyphs", fontPath);
                    return;
                }

                totalChecked += checkedEntries;
                totalMatched += matchedEntries;
                Pass(
                    "{0} lobby AXIS Hangul source entries verified: checked={1}, matched={2}",
                    fontPath,
                    checkedEntries,
                    matchedEntries);
            }

            private static Dictionary<uint, FdtGlyphEntry> ReadHangulGlyphEntries(byte[] fdt)
            {
                Dictionary<uint, FdtGlyphEntry> result = new Dictionary<uint, FdtGlyphEntry>();
                int fontTableOffset;
                uint glyphCount;
                int glyphStart;
                if (!TryGetFdtGlyphTable(fdt, out fontTableOffset, out glyphCount, out glyphStart))
                {
                    return result;
                }

                for (int i = 0; i < glyphCount; i++)
                {
                    int offset = glyphStart + i * FdtGlyphEntrySize;
                    uint codepoint;
                    if (!TryDecodeFdtUtf8Value(Endian.ReadUInt32LE(fdt, offset), out codepoint) ||
                        !IsHangulCodepoint(codepoint))
                    {
                        continue;
                    }

                    result[codepoint] = ReadGlyphEntry(fdt, offset);
                }

                return result;
            }

            private static bool LobbyAxisHangulAdvanceEntryMatchesExpected(
                FdtGlyphEntry sourceGlyph,
                FdtGlyphEntry targetGlyph)
            {
                return sourceGlyph.ShiftJisValue == targetGlyph.ShiftJisValue &&
                       sourceGlyph.ImageIndex == targetGlyph.ImageIndex &&
                       sourceGlyph.X == targetGlyph.X &&
                       sourceGlyph.Y == targetGlyph.Y &&
                       sourceGlyph.Width == targetGlyph.Width &&
                       sourceGlyph.Height == targetGlyph.Height &&
                       sourceGlyph.OffsetX == targetGlyph.OffsetX &&
                       sourceGlyph.OffsetY == targetGlyph.OffsetY;
            }

            private static string FormatGlyphEntryRoute(FdtGlyphEntry glyph)
            {
                return FormatGlyphSpacing(glyph) +
                       ", image=" + glyph.ImageIndex.ToString() +
                       ", xy=" + glyph.X.ToString() + "/" + glyph.Y.ToString();
            }

            private static bool IsLobbyAxisHangulAdvanceNormalizedFont(string fontPath)
            {
                string normalized = fontPath.Replace('\\', '/');
                return string.Equals(normalized, "common/font/AXIS_12_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/AXIS_14_lobby.fdt", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(normalized, "common/font/AXIS_18_lobby.fdt", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
