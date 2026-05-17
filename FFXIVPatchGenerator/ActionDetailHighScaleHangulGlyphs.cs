using System;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class ActionDetailHighScaleHangulGlyphs
    {
        public const string SourceFontPath = "common/font/TrumpGothic_34.fdt";
        public const string TargetFontPath = "common/font/TrumpGothic_68.fdt";
        public const double LargeUiHangulToDigitRatio = 1.08d;

        public const string InstantCastPhrase = "\uC989\uC2DC \uBC1C\uB3D9";
        public const string CastTimePhrase = "\uC2DC\uC804 \uC2DC\uAC04";
        public const string RecastPhrase = "\uC7AC\uC0AC\uC6A9";
        public const string RecastTimePhrase = "\uC7AC\uC0AC\uC6A9 \uB300\uAE30 \uC2DC\uAC04";
        public const string ActivationConditionPhrase = "\uBC1C\uB3D9 \uC870\uAC74";
        public const string SecondUnitPhrase = "\uCD08";
        public const string CriticalPhrase = "\uADF9\uB300\uD654";
        public const string CriticalShortPhrase = "\uADF9\uB300";
        public const string DirectHitPhrase = "\uC9C1\uACA9";
        public const string CriticalDirectHitPhrase = "\uADF9\uB300 \uC9C1\uACA9";
        public const string CriticalFlyTextPhrase = "\uD06C\uB9AC\uD2F0\uCEEC";
        public const string DutyFinderPhrase = "\uC784\uBB34 \uCC3E\uAE30";
        public const string QuestPhrase = "\uD018\uC2A4\uD2B8";
        public const string SystemConfigurationPhrase = "\uC2DC\uC2A4\uD15C \uC124\uC815";
        public const string CharacterPhrase = "\uCE90\uB9AD\uD130";
        public const string ActionsAndTraitsPhrase = "\uAE30\uC220 \uBC0F \uD2B9\uC131";
        public const string PvpProfilePhrase = "PvP \uD504\uB85C\uD544";
        public const string BattleRecordPhrase = "\uC804\uC801";
        public const string CrystallineConflictPhrase = "\uD06C\uB9AC\uC2A4\uD0C8\uB77C\uC778 \uCEE8\uD50C\uB9AD\uD2B8";
        public const string FrontlinePhrase = "\uC804\uC7A5";
        public const string RivalWingsPhrase = "\uACBD\uC7C1\uC758 \uB0A0\uAC1C";
        public const string PvpActionsPhrase = "PvP \uAE30\uC220";
        public const string TacticalCommunicationPhrase = "\uC804\uB7B5\uC801 \uB300\uD654";

        public static readonly string[] FallbackPhrases = new string[]
        {
            InstantCastPhrase,
            CastTimePhrase,
            RecastPhrase,
            RecastTimePhrase,
            ActivationConditionPhrase,
            SecondUnitPhrase,
            DutyFinderPhrase,
            QuestPhrase,
            SystemConfigurationPhrase,
            CharacterPhrase,
            ActionsAndTraitsPhrase,
            PvpProfilePhrase,
            BattleRecordPhrase,
            CrystallineConflictPhrase,
            FrontlinePhrase,
            RivalWingsPhrase,
            PvpActionsPhrase,
            TacticalCommunicationPhrase
        };

        public static readonly string[] LargeUiLabelSheetNames = new string[]
        {
            "Addon",
            "AddonTransient",
            "MainCommand",
            "ContentFinderCondition",
            "ContentRoulette",
            "ContentType"
        };

        public static readonly AddonRowRange[] AddonRowRanges = new AddonRowRange[]
        {
            new AddonRowRange(699, 714)
        };

        public static readonly string[] CombatFlyTextPreservePhrases = new string[]
        {
            CriticalPhrase,
            CriticalShortPhrase,
            DirectHitPhrase,
            CriticalDirectHitPhrase,
            CriticalFlyTextPhrase
        };

        public static readonly string[] VisualScaleTargetFontPaths = new string[]
        {
            "common/font/TrumpGothic_23.fdt",
            "common/font/TrumpGothic_34.fdt",
            "common/font/TrumpGothic_68.fdt"
        };

        public static bool IsTargetFontPath(string path)
        {
            return string.Equals(
                Normalize(path),
                TargetFontPath,
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsVisualScaleTargetFontPath(string path)
        {
            string normalized = Normalize(path);
            for (int i = 0; i < VisualScaleTargetFontPaths.Length; i++)
            {
                if (string.Equals(normalized, VisualScaleTargetFontPaths[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsCombatFlyTextPreservePhrase(string phrase)
        {
            phrase = phrase ?? string.Empty;
            for (int i = 0; i < CombatFlyTextPreservePhrases.Length; i++)
            {
                if (phrase.IndexOf(CombatFlyTextPreservePhrases[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }
    }
}
