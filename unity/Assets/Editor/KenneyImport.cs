using UnityEditor;

// Import rules for the Kenney kits: characters get legacy animation clips, everything else is static.
public class KenneyImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.Contains("/Kenney/")) return;
        var mi = (ModelImporter)assetImporter;
        mi.isReadable = !assetPath.Contains("/Cars/");   // city pieces are static-batched at runtime
        mi.meshCompression = ModelImporterMeshCompression.Medium;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importBlendShapes = false;
        if (assetPath.Contains("/Characters/"))
        {
            mi.animationType = ModelImporterAnimationType.Legacy;
            mi.importAnimation = true;
            mi.animationCompression = ModelImporterAnimationCompression.KeyframeReductionAndCompression;
        }
        else
        {
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
        }
    }

    // Bump to force every Kenney asset to reimport with these rules.
    public override uint GetVersion() => 2;

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Resources/")) return;
        var ti = (TextureImporter)assetImporter;
        if (assetPath.Contains("/Kenney/"))
        {
            // Kenney models colour themselves by sampling a tiny palette ("colormap"). Mipmaps, bilinear
            // filtering and block compression all blend neighbouring swatches, which muddies or swaps colours
            // (skin turns grey, blue crutches look like guns). Keep the palette exact.
            ti.mipmapEnabled = false;
            ti.filterMode = UnityEngine.FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
        ti.maxTextureSize = assetPath.Contains("/Icons/") ? 256 : 512;
        if (assetPath.Contains("/Icons/")) { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.alphaIsTransparency = true; ti.mipmapEnabled = false; }
    }
}
