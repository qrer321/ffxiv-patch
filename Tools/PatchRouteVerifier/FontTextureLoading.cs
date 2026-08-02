using System.IO;

namespace FfxivKoreanPatch.PatchRouteVerifier
{
    internal static partial class PatchRouteVerifier
    {
        private sealed partial class Verifier
        {
            private Texture ReadFontTexture(CompositeArchive archive, string texturePath)
            {
                string cacheKey = archive.CacheKey + "|" + texturePath;
                Texture texture;
                if (!_textureCache.TryGetValue(cacheKey, out texture))
                {
                    byte[] packed;
                    if (!archive.TryReadPackedFile(texturePath, out packed))
                    {
                        throw new FileNotFoundException("texture was not found", texturePath);
                    }

                    texture = ReadA4R4G4B4Texture(UnpackTextureFile(packed));
                    _textureCache.Add(cacheKey, texture);
                }

                return texture;
            }
        }
    }
}
