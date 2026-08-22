using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>손상된 동적 TMP 아틀라스를 원본 TTF로 다시 만들고 기존 에셋 GUID를 보존합니다.</summary>
public static class OmyuFontAssetRepair
{
    private const string FontPath = "Assets/Resources/omyuPretty.ttf";
    private const string AssetPath = "Assets/Resources/omyuPretty SDF.asset";

    public static void Repair()
    {
        Rebuild(FontPath, AssetPath, "omyuPretty SDF");
        Rebuild("Assets/Resources/Fonts/StoreV2/GowunBatang-Bold.ttf", "Assets/Resources/Fonts/StoreV2/GowunBatang-Bold SDF.asset", "GowunBatang-Bold SDF");
        Rebuild("Assets/Resources/Fonts/StoreV2/GowunDodum-Regular.ttf", "Assets/Resources/Fonts/StoreV2/GowunDodum-Regular SDF.asset", "GowunDodum-Regular SDF");
        ValidateAll();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void Rebuild(string fontPath, string assetPath, string name)
    {
        Font source = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (source == null) throw new InvalidOperationException($"원본 글꼴을 찾지 못했습니다: {fontPath}");
        TMP_FontAsset target = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (target == null) throw new InvalidOperationException($"복구할 TMP 글꼴 에셋을 찾지 못했습니다: {assetPath}");

        foreach (UnityEngine.Object subAsset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (subAsset != target && (subAsset is Texture2D || subAsset is Material))
                UnityEngine.Object.DestroyImmediate(subAsset, true);
        }

        TMP_FontAsset rebuilt = TMP_FontAsset.CreateFontAsset(source, 90, 5, GlyphRenderMode.SDFAA, 4096, 4096, AtlasPopulationMode.Dynamic, true);
        if (rebuilt == null || rebuilt.atlasTextures == null || rebuilt.atlasTextures.Length == 0 || rebuilt.atlasTextures[0] == null)
            throw new InvalidOperationException($"TMP 글꼴 아틀라스 생성에 실패했습니다: {fontPath}");

        rebuilt.name = name;
        Texture2D[] atlases = rebuilt.atlasTextures.Where(texture => texture != null).Distinct().ToArray();
        Material material = rebuilt.material;
        EditorUtility.CopySerialized(rebuilt, target);
        target.name = name;

        foreach (Texture2D atlas in atlases)
        {
            atlas.name = $"{name} Atlas";
            AssetDatabase.AddObjectToAsset(atlas, target);
            EditorUtility.SetDirty(atlas);
        }

        if (material != null)
        {
            material.name = $"{name} Material";
            AssetDatabase.AddObjectToAsset(material, target);
            EditorUtility.SetDirty(material);
        }

        EditorUtility.SetDirty(target);
        Debug.Log($"[TMP Atlas] REPAIRED {assetPath}");
    }

    private static void ValidateAll()
    {
        string[] paths =
        {
            AssetPath,
            "Assets/Resources/Fonts/StoreV2/GowunBatang-Bold SDF.asset",
            "Assets/Resources/Fonts/StoreV2/GowunDodum-Regular SDF.asset"
        };

        foreach (string path in paths)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null || font.atlasTextures == null || font.atlasTextures.Length == 0 || font.atlasTextures[0] == null || font.material == null)
                throw new InvalidOperationException($"TMP 글꼴 복구 검증 실패: {path}");
        }
    }

    public static void ReportInvalidAtlasFonts()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            bool invalid = font == null || font.atlasTextures == null || font.atlasTextures.Length == 0 || font.atlasTextures[0] == null;
            Debug.Log($"[TMP Atlas] {(invalid ? "INVALID" : "OK")} {path}");
        }
    }
}
