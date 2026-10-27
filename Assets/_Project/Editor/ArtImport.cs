using UnityEditor;
using UnityEngine;

namespace RallyAyiti.EditorTools
{
    /// <summary>Réglages d'import automatiques pour Assets/_Project/Art (textures et modèles).</summary>
    public sealed class ArtImport : AssetPostprocessor
    {
        private const string TexturesDir = "/_Project/Art/Textures/";
        private const string ModelsDir = "/_Project/Art/Models/";
        private const string AudioDir = "/_Project/Art/Audio/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains(TexturesDir)) return;
            var importer = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            importer.maxTextureSize = file.StartsWith("TapTap_Side") ? 2048 : 1024;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.wrapMode = file.StartsWith("Sign_") || file.StartsWith("TapTap_") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;

            if (file.EndsWith("_N"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (IsSky(file))
            {
                // Panorama équirectangulaire : pleine résolution, pas de mipmaps (évite la couture), pôles bloqués.
                importer.maxTextureSize = 4096;
                importer.mipmapEnabled = false;
                importer.anisoLevel = 0;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
            }
            else if (IsSprite(file))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                // Panneau glacé étirable sans déformer ses coins arrondis.
                if (file == "UI_Panel") importer.spriteBorder = new Vector4(48f, 48f, 48f, 48f);
            }
            else if (file == "Foam")
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
            }
            else if (file == "Water_Gradient")
            {
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
            }
            else if (IsCutout(file))
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.45f;
                if (file == "Pennants") importer.wrapModeV = TextureWrapMode.Clamp;
            }
        }

        /// <summary>Images d'interface : icônes, cartes de circuit, logo.</summary>
        public static bool IsSprite(string file) =>
            file.StartsWith("Icon_") || file.StartsWith("Card_") || file.StartsWith("UI_");

        /// <summary>Ciels panoramiques.</summary>
        public static bool IsSky(string file) => file.StartsWith("Sky_");

        /// <summary>Feuillages et fanions découpés par la transparence.</summary>
        public static bool IsCutout(string file) =>
            file is "Palm_Frond" or "Grass_Tuft" or "Banana_Leaf" or "Pennants";

        private void OnPreprocessAudio()
        {
            if (!assetPath.Contains(AudioDir)) return;
            var importer = (AudioImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool music = file.StartsWith("Music_") || file.StartsWith("Ambience_");
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            s.quality = 0.7f;
            importer.defaultSampleSettings = s;
            importer.forceToMono = !music;
            importer.loadInBackground = music;
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.Contains(ModelsDir)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.useFileScale = true;
            importer.globalScale = 1f;
        }
    }
}
