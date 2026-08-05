#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 编辑器窗口：从纹理中裁剪指定区域，生成新图片。
/// 用法：选中一张纹理 → Tools → 图片裁剪工具 → 拖拽框选区域 → 裁剪
/// </summary>
public class TextureCropWindow : EditorWindow
{
    private Texture2D _sourceTex;
    private string _sourcePath;
    private Rect _cropRect = Rect.zero; // 选择的裁剪区域（纹理坐标）
    private bool _isDragging = false;
    private Vector2 _dragStart;
    private Vector2 _scrollPos;
    private float _displayScale = 1f;
    private const float MAX_PREVIEW_WIDTH = 512f;

    [MenuItem("Tools/图片裁剪工具")]
    private static void Open()
    {
        var window = GetWindow<TextureCropWindow>("图片裁剪工具");
        window.minSize = new Vector2(600, 500);
        window.Show();
    }

    private void OnEnable()
    {
        LoadFromSelection();
    }

    private void OnSelectionChange()
    {
        LoadFromSelection();
        Repaint();
    }

    private void LoadFromSelection()
    {
        if (Selection.objects == null || Selection.objects.Length == 0)
            return;

        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
                continue;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                _sourceTex = tex;
                _sourcePath = path;
                _cropRect = Rect.zero;

                // 计算预览缩放
                _displayScale = Mathf.Min(1f, MAX_PREVIEW_WIDTH / tex.width);
                break;
            }
        }
    }

    private void OnGUI()
    {
        if (_sourceTex == null)
        {
            EditorGUILayout.HelpBox("请在 Project 窗口选中一张纹理图片", MessageType.Info);
            if (GUILayout.Button("刷新选中"))
                LoadFromSelection();
            return;
        }

        // 顶部信息
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label($"源图: {_sourceTex.name}  ({_sourceTex.width}×{_sourceTex.height})", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("刷新选中", EditorStyles.toolbarButton))
            LoadFromSelection();
        EditorGUILayout.EndHorizontal();

        // 裁剪区域输入
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("裁剪区域（可拖拽框选或手动输入）", EditorStyles.boldLabel);
        Rect oldRect = _cropRect;
        _cropRect.x = EditorGUILayout.FloatField("X", _cropRect.x);
        _cropRect.y = EditorGUILayout.FloatField("Y", _cropRect.y);
        _cropRect.width = EditorGUILayout.FloatField("Width", _cropRect.width);
        _cropRect.height = EditorGUILayout.FloatField("Height", _cropRect.height);

        // 快捷比例按钮
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("快捷:", GUILayout.Width(40));
        if (GUILayout.Button("1:1 正方形", GUILayout.Width(100)))
        {
            float size = Mathf.Min(_cropRect.width, _cropRect.height);
            if (size < 1f) size = Mathf.Min(_sourceTex.width, _sourceTex.height) * 0.5f;
            _cropRect.width = size;
            _cropRect.height = size;
        }
        if (GUILayout.Button("自动脸部（上1/3）", GUILayout.Width(120)))
        {
            _cropRect.x = 0;
            _cropRect.y = _sourceTex.height * 0.55f;
            _cropRect.width = _sourceTex.width;
            _cropRect.height = _sourceTex.height * 0.45f;
        }
        if (GUILayout.Button("全图", GUILayout.Width(60)))
        {
            _cropRect.x = 0;
            _cropRect.y = 0;
            _cropRect.width = _sourceTex.width;
            _cropRect.height = _sourceTex.height;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // 图片预览 + 拖拽框选
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        float previewW = _sourceTex.width * _displayScale;
        float previewH = _sourceTex.height * _displayScale;
        Rect previewRect = GUILayoutUtility.GetRect(previewW, previewH, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

        // 绘制纹理
        GUI.DrawTexture(previewRect, _sourceTex, ScaleMode.ScaleToFit);

        // 绘制裁剪框
        if (_cropRect.width > 0 && _cropRect.height > 0)
        {
            Rect displayCrop = new Rect(
                previewRect.x + _cropRect.x * _displayScale,
                previewRect.y + (_sourceTex.height - _cropRect.y - _cropRect.height) * _displayScale,
                _cropRect.width * _displayScale,
                _cropRect.height * _displayScale
            );

            // 半透明遮罩（裁剪框外部）
            Color maskColor = new Color(0, 0, 0, 0.4f);
            // 上
            GUI.color = maskColor;
            GUI.DrawTexture(new Rect(previewRect.x, previewRect.y, previewRect.width, displayCrop.y - previewRect.y), EditorGUIUtility.whiteTexture);
            // 下
            GUI.DrawTexture(new Rect(previewRect.x, displayCrop.yMax, previewRect.width, previewRect.yMax - displayCrop.yMax), EditorGUIUtility.whiteTexture);
            // 左
            GUI.DrawTexture(new Rect(previewRect.x, displayCrop.y, displayCrop.x - previewRect.x, displayCrop.height), EditorGUIUtility.whiteTexture);
            // 右
            GUI.DrawTexture(new Rect(displayCrop.xMax, displayCrop.y, previewRect.xMax - displayCrop.xMax, displayCrop.height), EditorGUIUtility.whiteTexture);
            GUI.color = Color.white;

            // 裁剪框边线
            Handles.color = Color.green;
            Handles.DrawSolidRectangleWithOutline(displayCrop, new Color(0, 1, 0, 0.01f), Color.green);
        }

        // 处理鼠标拖拽
        Event e = Event.current;
        if (e != null && previewRect.Contains(e.mousePosition))
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                _isDragging = true;
                _dragStart = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDragging)
            {
                Vector2 min = Vector2.Min(_dragStart, e.mousePosition);
                Vector2 max = Vector2.Max(_dragStart, e.mousePosition);

                // 转换到纹理坐标
                float texX = (min.x - previewRect.x) / _displayScale;
                float texY = _sourceTex.height - (max.y - previewRect.y) / _displayScale;
                float texW = (max.x - min.x) / _displayScale;
                float texH = (max.y - min.y) / _displayScale;

                _cropRect = new Rect(texX, texY, texW, texH);
                Repaint();
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _isDragging)
            {
                _isDragging = false;
                e.Use();
            }
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);

        // 输出信息
        if (_cropRect.width > 1 && _cropRect.height > 1)
        {
            int outW = Mathf.RoundToInt(_cropRect.width);
            int outH = Mathf.RoundToInt(_cropRect.height);
            EditorGUILayout.LabelField($"输出尺寸: {outW}×{outH}", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("裁剪并保存", GUILayout.Height(30)))
            {
                CropAndSave();
            }
            if (GUILayout.Button("裁剪为正方形（取较短边）", GUILayout.Height(30)))
            {
                float size = Mathf.Min(_cropRect.width, _cropRect.height);
                _cropRect.width = size;
                _cropRect.height = size;
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void CropAndSave()
    {
        if (_sourceTex == null || string.IsNullOrEmpty(_sourcePath))
        {
            EditorUtility.DisplayDialog("错误", "未选中有效的纹理图片", "确定");
            return;
        }

        if (_cropRect.width < 1 || _cropRect.height < 1)
        {
            EditorUtility.DisplayDialog("错误", "裁剪区域太小，请拖拽框选或输入有效区域", "确定");
            return;
        }

        // 确保 isReadable
        var importer = AssetImporter.GetAtPath(_sourcePath) as TextureImporter;
        bool wasReadable = false;
        bool wasChanged = false;
        if (importer != null && !importer.isReadable)
        {
            wasReadable = false;
            importer.isReadable = true;
            importer.SaveAndReimport();
            AssetDatabase.Refresh();
            wasChanged = true;
        }

        // 重新加载
        _sourceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(_sourcePath);
        if (_sourceTex == null)
        {
            EditorUtility.DisplayDialog("错误", "无法加载纹理", "确定");
            return;
        }

        // 裁剪
        int x = Mathf.Max(0, Mathf.RoundToInt(_cropRect.x));
        int y = Mathf.Max(0, Mathf.RoundToInt(_cropRect.y));
        int w = Mathf.Min(Mathf.RoundToInt(_cropRect.width), _sourceTex.width - x);
        int h = Mathf.Min(Mathf.RoundToInt(_cropRect.height), _sourceTex.height - y);

        if (w < 1 || h < 1)
        {
            EditorUtility.DisplayDialog("错误", "裁剪区域超出图片范围", "确定");
            return;
        }

        Color[] pixels = _sourceTex.GetPixels(x, y, w, h);
        var newTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        newTex.SetPixels(pixels);
        newTex.Apply();

        // 保存
        string dir = System.IO.Path.GetDirectoryName(_sourcePath);
        string name = System.IO.Path.GetFileNameWithoutExtension(_sourcePath);
        string outPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{name}_crop.png");

        byte[] bytes = newTex.EncodeToPNG();
        System.IO.File.WriteAllBytes(outPath, bytes);
        Object.DestroyImmediate(newTex);

        // 恢复 isReadable
        if (wasChanged && importer != null)
        {
            importer.isReadable = wasReadable;
            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh();
        Debug.Log($"已裁剪保存: {outPath} ({w}×{h})");

        // 选中新文件
        var newAsset = AssetDatabase.LoadAssetAtPath<Object>(outPath);
        if (newAsset != null)
            Selection.activeObject = newAsset;

        EditorUtility.DisplayDialog("完成", $"已裁剪保存: {outPath}\n尺寸: {w}×{h}", "确定");
    }
}
#endif
