using UnityEditor;

/// <summary>
/// Art 目录下的背景贴图自动设置为可读（引擎运行时要做灰度化 GetPixels），
/// 并关闭 mipmap、限制最大尺寸 1920。
/// </summary>
public class ArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Art/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.isReadable = true;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = 1920;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
