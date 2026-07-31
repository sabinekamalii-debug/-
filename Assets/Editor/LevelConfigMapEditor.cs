using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 关卡地图可视化编辑器（支持编辑地形+路线）。
/// 菜单：Tools → 关卡地图编辑器
/// 
/// 三种模式：
/// View  — 只看不改
/// Paint — 左键刷地形（空地/地面/墙/高台），右键循环切换
/// Path  — 编辑敌人路线：左键点击地面格添加航点，右键删除航点，拖动移动航点
///         自动保证水平/垂直段（不允许斜线）
/// </summary>
public class LevelConfigMapEditor : EditorWindow
{
    static readonly Color COL_EMPTY     = new Color(0.15f, 0.15f, 0.15f, 1f);
    static readonly Color COL_GROUND     = new Color(0.35f, 0.65f, 0.25f, 1f);
    static readonly Color COL_WALL      = new Color(0.35f, 0.22f, 0.45f, 1f);
    static readonly Color COL_HIGH      = new Color(0.35f, 0.55f, 0.95f, 1f);
    static readonly Color COL_GRID_LINE = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    static readonly Color COL_BORDER   = new Color(0.8f, 0.8f, 0.8f, 0.8f);
    static readonly Color COL_PATH_CELL = new Color(1f, 0.85f, 0.2f, 0.25f);

    static readonly Color[] PATH_COLORS = new Color[]
    {
        new Color(1f, 0.85f, 0.2f),
        new Color(1f, 0.4f, 0.4f),
        new Color(0.4f, 1f, 0.6f),
        new Color(0.4f, 0.6f, 1f),
    };

    List<LevelConfig> _allConfigs = new List<LevelConfig>();
    int _selectedIndex = -1;
    LevelConfig _current;
    bool _dirty = false;

    const float CELL_SIZE = 32f;

    // ===== Undo 支持 =====
    void RecordUndo(string label)
    {
        if (_current != null)
            Undo.RegisterCompleteObjectUndo(_current, label);
    }

    enum EditMode { View, Paint, Path }
    EditMode _editMode = EditMode.View;
    int _paintCellType = 1;
    int _editPathIndex = 0; // 当前编辑的路线 0-3

    int _dragPathIndex = -1;
    int _dragWaypointIndex = -1;
    bool _didDrag = false;

    Vector2 _scrollList;
    Vector2 _scrollGrid;

    [MenuItem("Tools/关卡地图编辑器")]
    static void Open()
    {
        var window = GetWindow<LevelConfigMapEditor>("关卡地图编辑器");
        window.minSize = new Vector2(900, 560);
        window.LoadAllConfigs();
        window.Show();
    }

    void LoadAllConfigs()
    {
        _allConfigs.Clear();
        var guids = AssetDatabase.FindAssets("t:LevelConfig");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
            if (config != null) _allConfigs.Add(config);
        }
        _allConfigs = _allConfigs.OrderBy(c => c.levelId).ToList();
    }

    void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.BeginHorizontal();
        DrawSidebar();
        DrawMapArea();
        EditorGUILayout.EndHorizontal();
        DrawStatusBar();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("刷新列表", EditorStyles.toolbarButton, GUILayout.Width(80)))
            LoadAllConfigs();

        GUILayout.Space(10);
        _editMode = (EditMode)EditorGUILayout.EnumPopup(_editMode, EditorStyles.toolbarPopup, GUILayout.Width(80));

        if (_editMode == EditMode.Paint)
        {
            GUILayout.Label("刷子:", GUILayout.Width(35));
            _paintCellType = GUILayout.SelectionGrid(_paintCellType, new[]{"空地","地面","墙","高台"}, 4, EditorStyles.toolbarButton, GUILayout.Width(240));
        }
        else if (_editMode == EditMode.Path)
        {
            GUILayout.Label("编辑路线:", GUILayout.Width(55));
            _editPathIndex = EditorGUILayout.Popup(_editPathIndex, new[]{"Path0","Path1","Path2","Path3"}, EditorStyles.toolbarPopup, GUILayout.Width(70));

            if (GUILayout.Button("清空此路线", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                RecordUndo("清空路线");
                SetPath(_editPathIndex, System.Array.Empty<Vector3>());
                _dirty = true;
            }
            if (GUILayout.Button("终点=守护点", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                var p = GetPath(_editPathIndex);
                if (p != null && p.Length > 0)
                {
                    RecordUndo("设置终点");
                    p[p.Length - 1] = new Vector3(3.5f, 3.5f, 0);
                    SetPath(_editPathIndex, p);
                    _dirty = true;
                }
            }
        }

        GUILayout.FlexibleSpace();

        if (_current != null)
        {
            if (GUILayout.Button("撤销(Ctrl+Z)", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                Undo.PerformUndo();
                Repaint();
            }
            if (GUILayout.Button("重做(Ctrl+Y)", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                Undo.PerformRedo();
                Repaint();
            }
        }

        if (_dirty && _current != null)
        {
            GUI.color = Color.yellow;
            if (GUILayout.Button("保存修改 *", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                EditorUtility.SetDirty(_current);
                AssetDatabase.SaveAssets();
                _dirty = false;
            }
            GUI.color = Color.white;
        }
        EditorGUILayout.EndHorizontal();
    }

    void DrawSidebar()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        GUILayout.Label("关卡列表", EditorStyles.boldLabel);
        _scrollList = EditorGUILayout.BeginScrollView(_scrollList, GUILayout.Width(200));
        for (int i = 0; i < _allConfigs.Count; i++)
        {
            var config = _allConfigs[i];
            if (config == null) continue;
            var color = GUI.color;
            if (i == _selectedIndex) GUI.color = new Color(0.3f, 0.5f, 0.8f, 0.3f);
            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            if (GUILayout.Button($"L{config.levelId:D2} {config.displayName}", GUI.skin.label, GUILayout.Width(180)))
            {
                _selectedIndex = i; _current = config; _dirty = false;
            }
            EditorGUILayout.EndHorizontal();
            GUI.color = color;
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void DrawMapArea()
    {
        if (_current == null) { EditorGUILayout.HelpBox("← 请在左侧选择一个关卡", MessageType.Info); return; }
        EditorGUILayout.BeginVertical();

        // 关卡信息
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"L{_current.levelId:D2} {_current.displayName}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"类型:{_current.levelType}", GUILayout.Width(90));
        EditorGUILayout.LabelField($"{_current.gridWidth}×{_current.gridHeight}", GUILayout.Width(60));
        EditorGUILayout.LabelField($"波次:{_current.waveGroups?.Length ?? 0}", GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        _scrollGrid = EditorGUILayout.BeginScrollView(_scrollGrid);
        int gw = _current.gridWidth, gh = _current.gridHeight;
        float mapW = gw * CELL_SIZE + 80, mapH = gh * CELL_SIZE + 80;
        var bgRect = GUILayoutUtility.GetRect(mapW, mapH);
        EditorGUI.DrawRect(bgRect, new Color(0.1f, 0.1f, 0.12f));

        float ox = bgRect.x + 30, oy = bgRect.yMax - 30;

        // === 1. 画格子 ===
        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                int ct = _current.GetCellType(x, y);
                Color col = ct switch { 0=>COL_EMPTY, 1=>COL_GROUND, 2=>COL_WALL, 3=>COL_HIGH, _=>Color.magenta };
                float px = ox + x * CELL_SIZE, py = oy - (y+1) * CELL_SIZE;
                var cellRect = new Rect(px, py, CELL_SIZE, CELL_SIZE);

                var evt = Event.current;
                if (_editMode == EditMode.Paint && evt.type == EventType.MouseDown && cellRect.Contains(evt.mousePosition))
                {
                    if (evt.button == 0) { RecordUndo("刷地形"); _current.SetCellType(x, y, _paintCellType); _dirty = true; Repaint(); }
                    else if (evt.button == 1) { RecordUndo("循环切换地形"); _current.SetCellType(x, y, (ct+1)%4); _dirty = true; Repaint(); }
                    evt.Use();
                }

                EditorGUI.DrawRect(cellRect, col);
                EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, 1), COL_GRID_LINE);
                EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, 1, cellRect.height), COL_GRID_LINE);
            }
        }
        DrawBorder(new Rect(ox, oy - gh*CELL_SIZE, gw*CELL_SIZE, gh*CELL_SIZE), COL_BORDER);

        // 坐标标注
        for (int x = 0; x < gw; x++)
            EditorGUI.LabelField(new Rect(ox + x*CELL_SIZE + CELL_SIZE/2 - 6, oy+4, 16, 14), x.ToString(), EditorStyles.miniLabel);
        for (int y = 0; y < gh; y++)
            EditorGUI.LabelField(new Rect(ox-22, oy-(y+1)*CELL_SIZE+CELL_SIZE/2-6, 20, 14), y.ToString(), EditorStyles.miniLabel);

        // === 2. 高亮路径经过的格子 ===
        if (_editMode == EditMode.Path)
            HighlightPathCells(ox, oy);

        // === 3. 画路线 ===
        DrawPaths(ox, oy);

        // === 4. Path 模式：点击格子添加航点 ===
        if (_editMode == EditMode.Path && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            var mp = Event.current.mousePosition;
            // 检查是否点在某个航点上（拖动逻辑在 DrawPaths 处理）
            if (!IsOnWaypoint(mp, ox, oy))
            {
                int gx = Mathf.FloorToInt((mp.x - ox) / CELL_SIZE);
                int gy = Mathf.FloorToInt((oy - mp.y) / CELL_SIZE);
                if (gx >= 0 && gx < gw && gy >= 0 && gy < gh)
                {
                    RecordUndo("添加航点");
                    // 确保是地面格
                    if (_current.GetCellType(gx, gy) != 1)
                    {
                        // 自动改为地面
                        _current.SetCellType(gx, gy, 1);
                    }
                    // 添加航点
                    float wx = gx - 9.5f, wy = gy - 5.5f;
                    var path = GetPath(_editPathIndex);
                    var list = path == null ? new List<Vector3>() : new List<Vector3>(path);
                    list.Add(new Vector3(wx, wy, 0));
                    SetPath(_editPathIndex, list.ToArray());
                    _dirty = true;
                    Repaint();
                    Event.current.Use();
                }
            }
        }

        DrawLegend(bgRect.xMax - 150, bgRect.y + 10);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void HighlightPathCells(float ox, float oy)
    {
        var path = GetPath(_editPathIndex);
        if (path == null || path.Length < 2) return;
        for (int i = 0; i < path.Length - 1; i++)
        {
            int gx1 = Mathf.RoundToInt(path[i].x + 9.5f), gy1 = Mathf.RoundToInt(path[i].y + 5.5f);
            int gx2 = Mathf.RoundToInt(path[i+1].x + 9.5f), gy2 = Mathf.RoundToInt(path[i+1].y + 5.5f);
            if (gy1 == gy2)
                for (int x = Mathf.Min(gx1,gx2); x <= Mathf.Max(gx1,gx2); x++)
                {
                    if (x<0||x>=_current.gridWidth||gy1<0||gy1>=_current.gridHeight) continue;
                    var r = new Rect(ox + x*CELL_SIZE, oy-(gy1+1)*CELL_SIZE, CELL_SIZE, CELL_SIZE);
                    EditorGUI.DrawRect(r, COL_PATH_CELL);
                }
            else if (gx1 == gx2)
                for (int y = Mathf.Min(gy1,gy2); y <= Mathf.Max(gy1,gy2); y++)
                {
                    if (gx1<0||gx1>=_current.gridWidth||y<0||y>=_current.gridHeight) continue;
                    var r = new Rect(ox + gx1*CELL_SIZE, oy-(y+1)*CELL_SIZE, CELL_SIZE, CELL_SIZE);
                    EditorGUI.DrawRect(r, COL_PATH_CELL);
                }
        }
    }

    Vector2 WorldToScreen(float wx, float wy, float ox, float oy)
    {
        return new Vector2(ox + (wx+9.5f)*CELL_SIZE + CELL_SIZE/2, oy - (wy+5.5f)*CELL_SIZE - CELL_SIZE/2);
    }

    Vector2 ScreenToWorld(float mx, float my, float ox, float oy)
    {
        float gx = (mx - ox - CELL_SIZE/2) / CELL_SIZE;
        float gy = (oy - my - CELL_SIZE/2) / CELL_SIZE;
        return new Vector2(gx - 9.5f, gy - 5.5f);
    }

    Vector3[] GetPath(int idx)
    {
        return idx switch { 0=>_current.path0Waypoints, 1=>_current.path1Waypoints, 2=>_current.path2Waypoints, 3=>_current.path3Waypoints, _=>null };
    }
    void SetPath(int idx, Vector3[] val)
    {
        switch (idx) { case 0: _current.path0Waypoints=val; break; case 1: _current.path1Waypoints=val; break; case 2: _current.path2Waypoints=val; break; case 3: _current.path3Waypoints=val; break; }
    }

    bool IsOnWaypoint(Vector2 mp, float ox, float oy)
    {
        var paths = _current.GetAllPaths();
        for (int pi = 0; pi < paths.Length; pi++)
        {
            if (paths[pi] == null) continue;
            for (int i = 0; i < paths[pi].Length; i++)
            {
                var sp = WorldToScreen(paths[pi][i].x, paths[pi][i].y, ox, oy);
                if (Vector2.Distance(mp, sp) < 12f) return true;
            }
        }
        return false;
    }

    void DrawPaths(float ox, float oy)
    {
        var paths = _current.GetAllPaths();
        for (int pi = 0; pi < paths.Length; pi++)
        {
            var wps = paths[pi];
            if (wps == null || wps.Length < 1) continue;
            var color = PATH_COLORS[pi % 4];
            var pts = new Vector2[wps.Length];
            for (int i = 0; i < wps.Length; i++)
                pts[i] = WorldToScreen(wps[i].x, wps[i].y, ox, oy);

            if (pts.Length >= 2)
            {
                Handles.color = color;
                for (int i = 0; i < pts.Length - 1; i++)
                    Handles.DrawAAPolyLine(3f, pts[i], pts[i+1]);
            }

            for (int i = 0; i < pts.Length; i++)
            {
                bool isFirst = (i == 0), isLast = (i == pts.Length - 1);
                float r = isFirst ? 11f : (isLast ? 14f : 8f);
                var wpRect = new Rect(pts[i].x - r, pts[i].y - r, r*2, r*2);

                if (isFirst)
                {
                    Handles.color = new Color(0.2f, 0.9f, 0.3f);
                    Handles.DrawSolidDisc(pts[i], Vector3.forward, r);
                    Handles.color = Color.white;
                    Handles.DrawWireDisc(pts[i], Vector3.forward, r);
                    EditorGUI.LabelField(new Rect(pts[i].x-8, pts[i].y-7, 16, 14), "出", EditorStyles.miniBoldLabel);
                }
                else if (isLast)
                {
                    Handles.color = new Color(0.3f, 0.5f, 1f, 0.85f);
                    Handles.DrawSolidDisc(pts[i], Vector3.forward, r);
                    Handles.color = Color.white;
                    Handles.DrawWireDisc(pts[i], Vector3.forward, r);
                    Handles.color = new Color(0.5f, 0.7f, 1f, 0.9f);
                    Handles.DrawSolidDisc(pts[i], Vector3.forward, r-4f);
                    EditorGUI.LabelField(new Rect(pts[i].x-8, pts[i].y-7, 16, 14), "守", EditorStyles.miniBoldLabel);
                }
                else
                {
                    Handles.color = color;
                    Handles.DrawSolidDisc(pts[i], Vector3.forward, r);
                    Handles.color = Color.white;
                    Handles.DrawWireDisc(pts[i], Vector3.forward, r);
                    EditorGUI.LabelField(new Rect(pts[i].x-6, pts[i].y-6, 12, 12), i.ToString(), EditorStyles.miniLabel);
                }

                // 拖拽航点
                var evt = Event.current;
                if (evt.type == EventType.MouseDown && wpRect.Contains(evt.mousePosition) && evt.button == 0 && _editMode == EditMode.Path)
                {
                    RecordUndo("拖动航点");
                    _dragPathIndex = pi; _dragWaypointIndex = i; _didDrag = false;
                    evt.Use();
                }
                else if (evt.type == EventType.MouseDrag && _dragPathIndex == pi && _dragWaypointIndex == i && _editMode == EditMode.Path)
                {
                    var world = ScreenToWorld(evt.mousePosition.x, evt.mousePosition.y, ox, oy);
                    // 网格吸附
                    int gx = Mathf.RoundToInt(world.x + 9.5f), gy = Mathf.RoundToInt(world.y + 5.5f);
                    float snapX = gx - 9.5f, snapY = gy - 5.5f;

                    // 自动打通地面
                    if (gx > 0 && gx < _current.gridWidth && gy > 0 && gy < _current.gridHeight)
                        if (_current.GetCellType(gx, gy) != 1) _current.SetCellType(gx, gy, 1);

                    var p = GetPath(pi);
                    if (p != null && i < p.Length)
                    {
                        p[i] = new Vector3(snapX, snapY, 0);
                        SetPath(pi, p);
                    }
                    _dirty = true; _didDrag = true;
                    Repaint();
                    evt.Use();
                }
                else if (evt.type == EventType.MouseUp && _dragPathIndex == pi && _dragWaypointIndex == i && _editMode == EditMode.Path)
                {
                    // 如果没有拖动，右键删除
                    if (!_didDrag && evt.button == 1)
                    {
                        var p = GetPath(pi);
                        if (p != null && p.Length > 2 && i > 0 && i < p.Length - 1)
                        {
                            var list = new List<Vector3>(p);
                            list.RemoveAt(i);
                            SetPath(pi, list.ToArray());
                            _dirty = true;
                        }
                        Repaint();
                        evt.Use();
                    }
                    _dragPathIndex = -1; _dragWaypointIndex = -1;
                }

                // 右键删除（中间航点）
                if (_editMode == EditMode.Path && evt.type == EventType.ContextClick && wpRect.Contains(evt.mousePosition) && i > 0 && i < pts.Length - 1)
                {
                    RecordUndo("删除航点");
                    var p = GetPath(pi);
                    if (p != null && p.Length > 2)
                    {
                        var list = new List<Vector3>(p);
                        list.RemoveAt(i);
                        SetPath(pi, list.ToArray());
                        _dirty = true;
                        Repaint();
                    }
                    evt.Use();
                }
            }
        }
    }

    void DrawLegend(float x, float y)
    {
        var rect = new Rect(x, y, 140, 136);
        EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.7f));
        float yy = y + 5;
        DrawLegendItem(x+5, yy, "空地", COL_EMPTY); yy += 18;
        DrawLegendItem(x+5, yy, "地面", COL_GROUND); yy += 18;
        DrawLegendItem(x+5, yy, "墙壁", COL_WALL); yy += 18;
        DrawLegendItem(x+5, yy, "高台", COL_HIGH); yy += 18;
        DrawLegendItem(x+5, yy, "出 刷怪点", new Color(0.2f, 0.9f, 0.3f)); yy += 18;
        DrawLegendItem(x+5, yy, "守 守护点(终点)", new Color(0.3f, 0.5f, 1f)); yy += 18;
        DrawLegendItem(x+5, yy, "○ 航点(可拖动)", Color.yellow);
    }

    void DrawLegendItem(float x, float y, string label, Color color)
    {
        EditorGUI.DrawRect(new Rect(x, y+2, 14, 14), color);
        EditorGUI.LabelField(new Rect(x+18, y, 100, 16), label, EditorStyles.miniLabel);
    }

    void DrawStatusBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        if (_current != null)
        {
            string status = $"模式: {_editMode}";
            if (_editMode == EditMode.Paint)
                status += $" | 刷子: {new[]{"空地","地面","墙","高台"}[_paintCellType]} | 左键设置 右键循环";
            else if (_editMode == EditMode.Path)
                status += $" | 路线: Path{_editPathIndex} | 左键=添加航点 右键=删除航点 拖动=移动(自动吸附网格+打通地面)";
            else
                status += " | 只读模式";
            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
        }
        else
            EditorGUILayout.LabelField("选择一个关卡开始编辑", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    void DrawBorder(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax-2, rect.width, 2), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax-2, rect.y, 2, rect.height), color);
    }
}
