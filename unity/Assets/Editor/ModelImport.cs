using UnityEditor;
using UnityEngine;

// §9 모델 에셋 임포트 설정 고정. Resources/Models/ 아래 PNG(colormap)는 ModelLib이 픽셀을 읽어 팔레트를 바꾸므로
// 읽기 가능·sRGB·비압축·포인트 필터·밉맵 없음이어야 한다. (.glb.bytes 는 TextAsset이라 따로 설정할 게 없다)
public class ModelImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').StartsWith("Assets/Resources/Models/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.isReadable = true;
        ti.sRGBTexture = true;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Point;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.alphaIsTransparency = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 2048;
    }
}
