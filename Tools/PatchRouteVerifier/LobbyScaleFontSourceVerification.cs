using System;
using System.Collections.Generic;
using FfxivKoreanPatch.FFXIVPatchGenerator;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static readonly LobbyScaleFontSourceRoute[] LobbyScaleFontSourceRoutes = new LobbyScaleFontSourceRoute[]
            {
                new LobbyScaleFontSourceRoute("common/font/AXIS_12_lobby.fdt", "common/font/AXIS_12_lobby.fdt"),
                new LobbyScaleFontSourceRoute("common/font/AXIS_14_lobby.fdt", "common/font/AXIS_14_lobby.fdt"),
                new LobbyScaleFontSourceRoute("common/font/AXIS_18_lobby.fdt", "common/font/AXIS_18_lobby.fdt"),
                new LobbyScaleFontSourceRoute("common/font/AXIS_36_lobby.fdt", "common/font/AXIS_36.fdt"),
                new LobbyScaleFontSourceRoute("common/font/Jupiter_46_lobby.fdt", "common/font/Jupiter_46.fdt"),
                new LobbyScaleFontSourceRoute("common/font/Jupiter_90_lobby.fdt", "common/font/Jupiter_46.fdt"),
                new LobbyScaleFontSourceRoute("common/font/Meidinger_40_lobby.fdt", "common/font/MiedingerMid_36.fdt"),
                new LobbyScaleFontSourceRoute("common/font/MiedingerMid_36_lobby.fdt", "common/font/MiedingerMid_36.fdt"),
                new LobbyScaleFontSourceRoute("common/font/TrumpGothic_68_lobby.fdt", "common/font/TrumpGothic_68.fdt")
            };

            private void VerifyLobbyScaleFontSourceRoutes()
            {
                Console.WriteLine("[FDT] Lobby scaled font source routes");
                if (_ttmpFont == null)
                {
                    Fail("TTMP font package is required to verify lobby scale source routes");
                    return;
                }

                uint[] codepoints = CollectLobbyScaleSensitiveHangulCodepoints();
                for (int routeIndex = 0; routeIndex < LobbyScaleFontSourceRoutes.Length; routeIndex++)
                {
                    VerifyLobbyScaleFontSourceRoute(LobbyScaleFontSourceRoutes[routeIndex], codepoints);
                }
            }

            private uint[] CollectLobbyScaleSensitiveHangulCodepoints()
            {
                HashSet<uint> codepoints = new HashSet<uint>();
                AddDynamicHangulCodepoints(codepoints, LobbyScaledHangulPhrases.StartScreenSystemSettings);
                AddDynamicHangulCodepoints(codepoints, LobbyScaledHangulPhrases.HighResolutionUiScaleOptions);
                AddDynamicHangulCodepoints(codepoints, LobbyScaledHangulPhrases.StartScreenSystemSettingsResultMessages);
                int staticCount = codepoints.Count;
                int addonDerived = AddPatchedAddonRangeHangulCodepoints(codepoints);
                uint[] values = new uint[codepoints.Count];
                codepoints.CopyTo(values);
                Array.Sort(values);
                Pass("lobby scale-sensitive Hangul codepoints collected: static={0}, addon-derived={1}, total={2}", staticCount, addonDerived, values.Length);
                return values;
            }

            private void VerifyLobbyScaleFontSourceRoute(LobbyScaleFontSourceRoute route, uint[] codepoints)
            {
                if (!_ttmpFont.ContainsPath(route.SourceFontPath))
                {
                    Fail("{0} source font is missing from TTMP package for scaled lobby target {1}", route.SourceFontPath, route.TargetFontPath);
                    return;
                }

                byte[] sourceFdt;
                byte[] targetFdt;
                try
                {
                    sourceFdt = _ttmpFont.ReadFile(route.SourceFontPath);
                    targetFdt = _patchedFont.ReadFile(route.TargetFontPath);
                }
                catch (Exception ex)
                {
                    Fail("{0} scaled lobby source-route read error: {1}", route.TargetFontPath, ex.Message);
                    return;
                }

                int checkedGlyphs = 0;
                for (int codepointIndex = 0; codepointIndex < codepoints.Length; codepointIndex++)
                {
                    uint codepoint = codepoints[codepointIndex];
                    if (!VerifyLobbyScaleFontSourceGlyph(route, sourceFdt, targetFdt, codepoint))
                    {
                        return;
                    }

                    checkedGlyphs++;
                }

                Pass("{0} scaled lobby Hangul glyphs match {1}: glyphs={2}", route.TargetFontPath, route.SourceFontPath, checkedGlyphs);
            }

            private bool VerifyLobbyScaleFontSourceGlyph(LobbyScaleFontSourceRoute route, byte[] sourceFdt, byte[] targetFdt, uint codepoint)
            {
                FdtGlyphEntry sourceGlyph;
                if (!TryFindGlyph(sourceFdt, codepoint, out sourceGlyph))
                {
                    Fail("{0} scaled lobby source {1} is missing U+{2:X4}", route.TargetFontPath, route.SourceFontPath, codepoint);
                    return false;
                }

                FdtGlyphEntry targetGlyph;
                if (!TryFindGlyph(targetFdt, codepoint, out targetGlyph))
                {
                    Fail("{0} scaled lobby target is missing U+{1:X4} from source {2}", route.TargetFontPath, codepoint, route.SourceFontPath);
                    return false;
                }

                if (targetGlyph.Width != sourceGlyph.Width ||
                    targetGlyph.Height != sourceGlyph.Height)
                {
                    Fail(
                        "{0} U+{1:X4} scaled lobby glyph size differs from {2}: target={3}x{4}, source={5}x{6}",
                        route.TargetFontPath,
                        codepoint,
                        route.SourceFontPath,
                        targetGlyph.Width,
                        targetGlyph.Height,
                        sourceGlyph.Width,
                        sourceGlyph.Height);
                    return false;
                }

                if (!GlyphSpacingMetricsMatch(sourceGlyph, targetGlyph))
                {
                    Fail(
                        "{0} U+{1:X4} scaled lobby glyph metrics differ from {2}: target={3}, source={4}",
                        route.TargetFontPath,
                        codepoint,
                        route.SourceFontPath,
                        FormatGlyphSpacing(targetGlyph),
                        FormatGlyphSpacing(sourceGlyph));
                    return false;
                }

                GlyphCanvas sourceCanvas;
                GlyphCanvas targetCanvas;
                try
                {
                    sourceCanvas = RenderGlyph(_ttmpFont, route.SourceFontPath, codepoint);
                    targetCanvas = RenderGlyph(_patchedFont, route.TargetFontPath, codepoint);
                }
                catch (Exception ex)
                {
                    Fail("{0} U+{1:X4} scaled lobby render error: {2}", route.TargetFontPath, codepoint, ex.Message);
                    return false;
                }

                if (sourceCanvas.VisiblePixels < 10 || targetCanvas.VisiblePixels < 10)
                {
                    Fail(
                        "{0} U+{1:X4} scaled lobby glyph visibility is too low: target={2}, source={3}",
                        route.TargetFontPath,
                        codepoint,
                        targetCanvas.VisiblePixels,
                        sourceCanvas.VisiblePixels);
                    return false;
                }

                long diff = Diff(sourceCanvas.Alpha, targetCanvas.Alpha);
                if (diff != 0)
                {
                    Fail(
                        "{0} U+{1:X4} scaled lobby glyph pixels differ from {2}: score={3}, visible={4}/{5}",
                        route.TargetFontPath,
                        codepoint,
                        route.SourceFontPath,
                        diff,
                        targetCanvas.VisiblePixels,
                        sourceCanvas.VisiblePixels);
                    return false;
                }

                string error;
                if (!VerifyGlyphTextureNeighborhoodMatchesTtmpSource(route.SourceFontPath, route.TargetFontPath, codepoint, DataCenterGlyphTexturePadding, out error))
                {
                    Fail(
                        "{0} U+{1:X4} scaled lobby glyph texture padding differs from {2}: {3}",
                        route.TargetFontPath,
                        codepoint,
                        route.SourceFontPath,
                        error);
                    return false;
                }

                return true;
            }

            private struct LobbyScaleFontSourceRoute
            {
                public readonly string TargetFontPath;
                public readonly string SourceFontPath;

                public LobbyScaleFontSourceRoute(string targetFontPath, string sourceFontPath)
                {
                    TargetFontPath = targetFontPath;
                    SourceFontPath = sourceFontPath;
                }
            }
        }
    }
}
