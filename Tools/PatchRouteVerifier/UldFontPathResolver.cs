namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private const byte UldAxisFontId = 0;
        private const byte UldMiedingerMedFontId = 1;
        private const byte UldMeidingerFontId = 2;
        private const byte UldTrumpGothicFontId = 3;
        private const byte UldJupiterFontId = 4;

        private static readonly UldFontSizeAlias[] EmptyUldFontSizeAliases = new UldFontSizeAlias[0];

        private static readonly UldFontFamilyRoute[] UldFontFamilyRoutes = new UldFontFamilyRoute[]
        {
            new UldFontFamilyRoute(
                UldAxisFontId,
                "common/font/AXIS_",
                new byte[] { 12, 14, 18, 36, 96 },
                new UldFontSizeAlias[] { new UldFontSizeAlias(34, 36) }),
            new UldFontFamilyRoute(
                UldMiedingerMedFontId,
                "common/font/MiedingerMid_",
                new byte[] { 10, 12, 14, 18, 36 },
                EmptyUldFontSizeAliases),
            new UldFontFamilyRoute(
                UldMeidingerFontId,
                "common/font/Meidinger_",
                new byte[] { 16, 20, 40 },
                EmptyUldFontSizeAliases),
            new UldFontFamilyRoute(
                UldTrumpGothicFontId,
                "common/font/TrumpGothic_",
                new byte[] { 23, 34, 68, 184 },
                EmptyUldFontSizeAliases),
            new UldFontFamilyRoute(
                UldJupiterFontId,
                "common/font/Jupiter_",
                new byte[] { 16, 20, 23, 45, 46, 90 },
                new UldFontSizeAlias[]
                {
                    new UldFontSizeAlias(12, 16),
                    new UldFontSizeAlias(18, 20)
                })
        };

        private static string ResolveUldFontPath(byte fontId, byte fontSize, bool lobby)
        {
            string suffix = lobby ? "_lobby.fdt" : ".fdt";
            for (int i = 0; i < UldFontFamilyRoutes.Length; i++)
            {
                UldFontFamilyRoute route = UldFontFamilyRoutes[i];
                int resolvedFontSize;
                if (route.FontId == fontId && route.TryResolveFontSize(fontSize, out resolvedFontSize))
                {
                    return route.PathPrefix + resolvedFontSize.ToString() + suffix;
                }
            }

            return null;
        }

        private struct UldFontFamilyRoute
        {
            public readonly byte FontId;
            public readonly string PathPrefix;
            private readonly byte[] _directFontSizes;
            private readonly UldFontSizeAlias[] _sizeAliases;

            public UldFontFamilyRoute(
                byte fontId,
                string pathPrefix,
                byte[] directFontSizes,
                UldFontSizeAlias[] sizeAliases)
            {
                FontId = fontId;
                PathPrefix = pathPrefix;
                _directFontSizes = directFontSizes;
                _sizeAliases = sizeAliases;
            }

            public bool TryResolveFontSize(byte fontSize, out int resolvedFontSize)
            {
                for (int i = 0; i < _sizeAliases.Length; i++)
                {
                    UldFontSizeAlias alias = _sizeAliases[i];
                    if (alias.UldFontSize == fontSize)
                    {
                        resolvedFontSize = alias.FileFontSize;
                        return true;
                    }
                }

                for (int i = 0; i < _directFontSizes.Length; i++)
                {
                    if (_directFontSizes[i] == fontSize)
                    {
                        resolvedFontSize = fontSize;
                        return true;
                    }
                }

                resolvedFontSize = 0;
                return false;
            }
        }

        private struct UldFontSizeAlias
        {
            public readonly byte UldFontSize;
            public readonly int FileFontSize;

            public UldFontSizeAlias(byte uldFontSize, int fileFontSize)
            {
                UldFontSize = uldFontSize;
                FileFontSize = fileFontSize;
            }
        }
    }
}
