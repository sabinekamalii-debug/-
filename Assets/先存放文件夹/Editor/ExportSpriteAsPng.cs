#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 Unity 里用 Sprite Editor 切出来的精灵导出为单独一张 PNG，像素不变、不降清晰度。
/// 用法：在 Project 里选中一个 Sprite（子资源），菜单 资源 → Export Sprite as PNG。
/// </summary>
public static class ExportSpriteAsPng
{
    private const string MenuName = "资源/Export Sprite as PNG";

    [MenuItem(MenuName, true)]
    private static bool ValidateExport()
    {
        if (Selection.activeObject == null) return false;
        return Selection.activeObject is Sprite;
    }

    [MenuItem(MenuName, false, 0)]
    private static void ExportSprite()
    {
        if (Selection.activeObject is not Sprite sprite)
        {
            EditorUtility.DisplayDialog("Export Sprite", "请先在 Project 里选中一个 Sprite（切出来的子资源）。", "确定");
            return;
        }

        string texturePath = AssetDatabase.GetAssetPath(sprite.texture);
        if (string.IsNullOrEmpty(texturePath))
        {
            EditorUtility.DisplayDialog("Export Sprite", "无法获取纹理路径。", "确定");
            return;
        }

        string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", texturePath));
        if (!File.Exists(fullPath))
        {
            EditorUtility.DisplayDialog("Export Sprite", "找不到原始图片文件：\n" + fullPath, "确定");
            return;
        }

        byte[] fileBytes = File.ReadAllBytes(fullPath);
        Texture2D fullTex = new Texture2D(2, 2);
        if (!fullTex.LoadImage(fileBytes))
        {
            UnityEngine.Object.DestroyImmediate(fullTex);
            EditorUtility.DisplayDialog("Export Sprite", "无法解析原始图片。", "确定");
            return;
        }

        Texture2D tex = sprite.texture;
        Rect r = sprite.rect;
        float scaleX = fullTex.width / (float)tex.width;
        float scaleY = fullTex.height / (float)tex.height;
        int x = Mathf.Clamp((int)(r.x * scaleX), 0, fullTex.width - 1);
        int y = Mathf.Clamp((int)(r.y * scaleY), 0, fullTex.height - 1);
        int w = Mathf.Min((int)(r.width * scaleX), fullTex.width - x);
        int h = Mathf.Min((int)(r.height * scaleY), fullTex.height - y);

        if (w <= 0 || h <= 0)
        {
            UnityEngine.Object.DestroyImmediate(fullTex);
            EditorUtility.DisplayDialog("Export Sprite", "精灵区域无效。", "确定");
            return;
        }

        string defaultDir = Application.dataPath;
        string defaultName = sprite.name + ".png";
        string savePath = EditorUtility.SaveFilePanel(
            "导出 Sprite 为 PNG",
            defaultDir,
            System.IO.Path.GetFileNameWithoutExtension(defaultName),
            "png"
        );

        if (string.IsNullOrEmpty(savePath))
        {
            UnityEngine.Object.DestroyImmediate(fullTex);
            return;
        }

        Color[] pixels = fullTex.GetPixels(x, y, w, h);
        UnityEngine.Object.DestroyImmediate(fullTex);
        Texture2D outTex = new Texture2D(w, h);
        outTex.SetPixels(pixels);
        outTex.Apply();

        string dir = System.IO.Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(savePath, outTex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(outTex);

        if (savePath.StartsWith(Application.dataPath))
            AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Export Sprite", $"已导出：{savePath}\n尺寸：{w}×{h} 像素（按原始图切割尺寸）。", "确定");
        if (savePath.StartsWith(Application.dataPath))
        {
            string relativePath = "Assets" + savePath.Substring(Application.dataPath.Length).Replace('\\', '/');
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(relativePath));
        }
    }
}
#endif
