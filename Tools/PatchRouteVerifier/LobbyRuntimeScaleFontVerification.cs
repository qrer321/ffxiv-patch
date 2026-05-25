using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private static readonly LobbyRuntimeScaleCase[] LobbyRuntimeScaleCases = new LobbyRuntimeScaleCase[]
            {
                new LobbyRuntimeScaleCase("start-system-title-23-lobby", "common/font/TrumpGothic_23_lobby.fdt", "시스템 설정", "common/font/AXIS_18.fdt", "システムコンフィグ", 0.88d, 1.24d, 0.62d, 1.40d, 0.62d, 1.40d),
                new LobbyRuntimeScaleCase("start-system-title-34-lobby", "common/font/TrumpGothic_34_lobby.fdt", "시스템 설정", "common/font/AXIS_36.fdt", "システムコンフィグ", 0.84d, 1.24d, 0.62d, 1.40d, 0.62d, 1.40d),
                new LobbyRuntimeScaleCase("start-system-title-68-lobby", "common/font/TrumpGothic_68_lobby.fdt", "시스템 설정", "common/font/AXIS_36.fdt", "システムコンフィグ", 0.88d, 1.24d, 0.62d, 1.40d, 0.62d, 1.40d),
                new LobbyRuntimeScaleCase("character-race-23", "common/font/TrumpGothic_23_lobby.fdt", "로스가르 여", "common/font/AXIS_18.fdt", "ロスガル", 0.88d, 1.24d, 0.62d, 1.40d, 0.62d, 1.40d),
                new LobbyRuntimeScaleCase("character-class-23", "common/font/TrumpGothic_23_lobby.fdt", "레벨 100 암흑기사", "common/font/AXIS_18.fdt", "レベル100暗黒騎士", 0.88d, 1.24d, 0.62d, 1.40d, 0.62d, 1.40d),
                new LobbyRuntimeScaleCase("character-race-34", "common/font/TrumpGothic_34_lobby.fdt", "로스가르 여", "common/font/AXIS_36.fdt", "ロスガル", 0.84d, 1.24d, 0.62d, 1.40d, 0.62d, 1.40d),
                new LobbyRuntimeScaleCase("character-class-34", "common/font/TrumpGothic_34_lobby.fdt", "레벨 100 암흑기사", "common/font/AXIS_36.fdt", "レベル100暗黒騎士", 0.84d, 1.24d, 0.62d, 1.40d, 0.62d, 1.40d),
                new LobbyRuntimeScaleCase("character-race-68", "common/font/TrumpGothic_68_lobby.fdt", "로스가르 여", "common/font/AXIS_36.fdt", "ロスガル", 0.88d, 1.24d, 0.62d, 1.40d, 0.62d, 1.40d),
                new LobbyRuntimeScaleCase("character-class-68", "common/font/TrumpGothic_68_lobby.fdt", "레벨 100 암흑기사", "common/font/AXIS_36.fdt", "レベル100暗黒騎士", 0.88d, 1.24d, 0.62d, 1.40d, 0.62d, 1.40d)
            };

            private void VerifyLobbyRuntimeScaleFontRoutes()
            {
                Console.WriteLine("[FDT] Lobby runtime scale font routes");
                for (int i = 0; i < LobbyRuntimeScaleCases.Length; i++)
                {
                    VerifyLobbyRuntimeScaleCase(LobbyRuntimeScaleCases[i]);
                }
            }

            private void VerifyLobbyRuntimeScaleCase(LobbyRuntimeScaleCase testCase)
            {
                PhraseVisualBounds korean;
                string error;
                if (!TryMeasurePhraseVisualBounds(_patchedFont, testCase.FontPath, testCase.KoreanPhrase, true, out korean, out error))
                {
                    Fail("{0} lobby runtime-scale [{1}] Korean phrase [{2}] layout error: {3}", testCase.FontPath, testCase.Id, Escape(testCase.KoreanPhrase), error);
                    return;
                }

                PhraseVisualBounds reference;
                if (!TryMeasurePhraseVisualBounds(_cleanFont, testCase.ReferenceFontPath, testCase.ReferencePhrase, false, out reference, out error))
                {
                    Fail("{0} lobby runtime-scale [{1}] clean reference layout error in {2}: clean=[{3}], error={4}", testCase.FontPath, testCase.Id, testCase.ReferenceFontPath, Escape(testCase.ReferencePhrase), error);
                    return;
                }

                double referenceHeight = reference.MeanReferenceHeight;
                double referenceWidth = reference.MeanReferenceWidth;
                double referenceAdvance = reference.MeanReferenceAdvance;
                if (korean.MeanHangulHeight <= 0d || referenceHeight <= 0d)
                {
                    Fail("{0} lobby runtime-scale [{1}] cannot compare height: korean={2}, reference={3}", testCase.FontPath, testCase.Id, FormatDouble(korean.MeanHangulHeight), FormatDouble(referenceHeight));
                    return;
                }

                bool heightOk = VerifyLobbyRuntimeScaleAxis(testCase, "height", korean.MeanHangulHeight, referenceHeight, testCase.MinHeightRatio, testCase.MaxHeightRatio, korean, reference);
                bool widthOk = VerifyLobbyRuntimeScaleAxis(testCase, "width", korean.MeanHangulWidth, referenceWidth, testCase.MinWidthRatio, testCase.MaxWidthRatio, korean, reference);
                bool advanceOk = VerifyLobbyRuntimeScaleAxis(testCase, "advance", korean.MeanHangulAdvance, referenceAdvance, testCase.MinAdvanceRatio, testCase.MaxAdvanceRatio, korean, reference);
                if (!heightOk || !widthOk || !advanceOk)
                {
                    return;
                }

                Pass(
                    "{0} lobby runtime-scale [{1}] matches clean scale: height={2}, width={3}, advance={4}, korean=[{5}], reference=[{6}]",
                    testCase.FontPath,
                    testCase.Id,
                    FormatRatio(SafeRatio(korean.MeanHangulHeight, referenceHeight)),
                    FormatRatio(SafeRatio(korean.MeanHangulWidth, referenceWidth)),
                    FormatRatio(SafeRatio(korean.MeanHangulAdvance, referenceAdvance)),
                    Escape(testCase.KoreanPhrase),
                    Escape(testCase.ReferencePhrase));
            }

            private bool VerifyLobbyRuntimeScaleAxis(
                LobbyRuntimeScaleCase testCase,
                string axis,
                double koreanValue,
                double referenceValue,
                double minimum,
                double maximum,
                PhraseVisualBounds korean,
                PhraseVisualBounds reference)
            {
                if (referenceValue <= 0d || koreanValue <= 0d)
                {
                    Fail("{0} lobby runtime-scale [{1}] {2} comparison has empty value: korean={3}, reference={4}", testCase.FontPath, testCase.Id, axis, FormatDouble(koreanValue), FormatDouble(referenceValue));
                    return false;
                }

                double ratio = SafeRatio(koreanValue, referenceValue);
                if (ratio < minimum || ratio > maximum)
                {
                    Fail(
                        "{0} lobby runtime-scale [{1}] {2} ratio {3} outside {4}..{5}: korean=[{6}] {7}, reference=[{8}] {9}, koreanBounds={10}, referenceBounds={11}",
                        testCase.FontPath,
                        testCase.Id,
                        axis,
                        FormatRatio(ratio),
                        FormatRatio(minimum),
                        FormatRatio(maximum),
                        Escape(testCase.KoreanPhrase),
                        FormatDouble(koreanValue),
                        Escape(testCase.ReferencePhrase),
                        FormatDouble(referenceValue),
                        FormatPhraseBounds(korean),
                        FormatPhraseBounds(reference));
                    return false;
                }

                return true;
            }

            private struct LobbyRuntimeScaleCase
            {
                public readonly string Id;
                public readonly string FontPath;
                public readonly string KoreanPhrase;
                public readonly string ReferenceFontPath;
                public readonly string ReferencePhrase;
                public readonly double MinHeightRatio;
                public readonly double MaxHeightRatio;
                public readonly double MinWidthRatio;
                public readonly double MaxWidthRatio;
                public readonly double MinAdvanceRatio;
                public readonly double MaxAdvanceRatio;

                public LobbyRuntimeScaleCase(
                    string id,
                    string fontPath,
                    string koreanPhrase,
                    string referenceFontPath,
                    string referencePhrase,
                    double minHeightRatio,
                    double maxHeightRatio,
                    double minWidthRatio,
                    double maxWidthRatio,
                    double minAdvanceRatio,
                    double maxAdvanceRatio)
                {
                    Id = id;
                    FontPath = fontPath;
                    KoreanPhrase = koreanPhrase;
                    ReferenceFontPath = referenceFontPath;
                    ReferencePhrase = referencePhrase;
                    MinHeightRatio = minHeightRatio;
                    MaxHeightRatio = maxHeightRatio;
                    MinWidthRatio = minWidthRatio;
                    MaxWidthRatio = maxWidthRatio;
                    MinAdvanceRatio = minAdvanceRatio;
                    MaxAdvanceRatio = maxAdvanceRatio;
                }
            }
        }
    }
}
