using System;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const byte UldAxisFontId = 0;
        private const byte UldMiedingerMedFontId = 1;
        private const byte UldMeidingerFontId = 2;
        private const byte UldTrumpGothicFontId = 3;
        private const byte UldJupiterFontId = 4;
        private const byte UldJupiterLargeFontId = 5;

        private static readonly UldFontFamilyRoute[] UldFontFamilyRoutes = new UldFontFamilyRoute[]
        {
            new UldFontFamilyRoute(
                UldAxisFontId,
                "common/font/AXIS_",
                new UldFontTier[]
                {
                    new UldFontTier(96, 9.6d),
                    new UldFontTier(12, 12d),
                    new UldFontTier(14, 14d),
                    new UldFontTier(18, 18d),
                    new UldFontTier(36, 36d)
                }),
            new UldFontFamilyRoute(
                UldMiedingerMedFontId,
                "common/font/MiedingerMid_",
                new UldFontTier[]
                {
                    new UldFontTier(10, 10d),
                    new UldFontTier(12, 12d),
                    new UldFontTier(14, 14d),
                    new UldFontTier(18, 18d),
                    new UldFontTier(36, 36d)
                }),
            new UldFontFamilyRoute(
                UldMeidingerFontId,
                "common/font/Meidinger_",
                new UldFontTier[]
                {
                    new UldFontTier(16, 16d),
                    new UldFontTier(20, 20d),
                    new UldFontTier(40, 40d)
                }),
            new UldFontFamilyRoute(
                UldTrumpGothicFontId,
                "common/font/TrumpGothic_",
                new UldFontTier[]
                {
                    new UldFontTier(184, 18.4d),
                    new UldFontTier(23, 23d),
                    new UldFontTier(34, 34d),
                    new UldFontTier(68, 68d)
                }),
            new UldFontFamilyRoute(
                UldJupiterFontId,
                "common/font/Jupiter_",
                new UldFontTier[]
                {
                    new UldFontTier(16, 16d),
                    new UldFontTier(20, 20d),
                    new UldFontTier(23, 23d),
                    new UldFontTier(46, 46d)
                }),
            new UldFontFamilyRoute(
                UldJupiterLargeFontId,
                "common/font/Jupiter_",
                new UldFontTier[]
                {
                    new UldFontTier(45, 45d),
                    new UldFontTier(90, 90d)
                })
        };

        private static string ResolveUldFontPath(byte fontId, byte fontSize, bool lobby)
        {
            return ResolveUldFontPathAtScale(fontId, fontSize, 100, lobby);
        }

        private static string ResolveUldFontPathAtScale(byte fontId, byte fontSize, int uiScalePercent, bool lobby)
        {
            if (uiScalePercent <= 0)
            {
                return null;
            }

            double requestedSize = fontSize * (uiScalePercent / 100d);
            string suffix = lobby ? "_lobby.fdt" : ".fdt";
            for (int i = 0; i < UldFontFamilyRoutes.Length; i++)
            {
                UldFontFamilyRoute route = UldFontFamilyRoutes[i];
                int fileFontSize;
                if (route.FontId == fontId && route.TryResolveClosestTier(requestedSize, out fileFontSize))
                {
                    return route.PathPrefix + fileFontSize.ToString() + suffix;
                }
            }

            return null;
        }

        private struct UldFontFamilyRoute
        {
            public readonly byte FontId;
            public readonly string PathPrefix;
            private readonly UldFontTier[] _tiers;

            public UldFontFamilyRoute(byte fontId, string pathPrefix, UldFontTier[] tiers)
            {
                FontId = fontId;
                PathPrefix = pathPrefix;
                _tiers = tiers;
            }

            public bool TryResolveClosestTier(double requestedSize, out int fileFontSize)
            {
                fileFontSize = 0;
                if (_tiers == null || _tiers.Length == 0)
                {
                    return false;
                }

                UldFontTier best = _tiers[0];
                double bestDistance = Math.Abs(requestedSize - best.NominalSize);
                for (int i = 1; i < _tiers.Length; i++)
                {
                    UldFontTier candidate = _tiers[i];
                    double distance = Math.Abs(requestedSize - candidate.NominalSize);
                    if (distance < bestDistance ||
                        (Math.Abs(distance - bestDistance) < 0.0001d && candidate.NominalSize > best.NominalSize))
                    {
                        best = candidate;
                        bestDistance = distance;
                    }
                }

                fileFontSize = best.FileFontSize;
                return true;
            }
        }

        private struct UldFontTier
        {
            public readonly int FileFontSize;
            public readonly double NominalSize;

            public UldFontTier(int fileFontSize, double nominalSize)
            {
                FileFontSize = fileFontSize;
                NominalSize = nominalSize;
            }
        }
    }
}
