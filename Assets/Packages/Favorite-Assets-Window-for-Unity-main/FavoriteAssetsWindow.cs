using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public class FavoriteAssetsWindow : EditorWindow
{
    private List<string> favoriteAssetPaths = new List<string>(); // Store asset paths
    private Vector2 scrollPos;
    private Object contextMenuAsset;
    private Object lastClickedAsset; // Track last clicked item

    private const string PREF_KEY = "FavoriteAssets"; // Save key
    private float lastClickTime = 0f;
    private const float doubleClickTime = 0.4f;

    [MenuItem("Window/#资产收藏夹")]
    public static void ShowWindow()
    {
        FavoriteAssetsWindow window = GetWindow<FavoriteAssetsWindow>("#资产收藏夹");
        Texture2D icon = EditorGUIUtility.IconContent("Favorite Icon").image as Texture2D;
        window.titleContent = new GUIContent("#资产收藏夹", icon);
    }

    [MenuItem("Assets/添加到#资产收藏夹", false, 20)]
    private static void AddToFavorites() {
        FavoriteAssetsWindow window = GetWindow<FavoriteAssetsWindow>("#资产收藏夹");

        foreach (Object obj in Selection.objects) {
            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && !window.favoriteAssetPaths.Contains(path)) {
                window.favoriteAssetPaths.Add(path);
            }
        }

        window.SaveFavorites();
        window.Repaint();
    }

    [MenuItem("Assets/移除自#资产收藏夹", false, 21)]
    private static void RemoveFromFavorites() {
        FavoriteAssetsWindow window = GetWindow<FavoriteAssetsWindow>("#资产收藏夹");

        foreach (Object obj in Selection.objects) {
            string path = AssetDatabase.GetAssetPath(obj);
            if (window.favoriteAssetPaths.Contains(path)) {
                window.favoriteAssetPaths.Remove(path);
            }
        }

        window.SaveFavorites();
        window.Repaint();
    }

    // Called when the window is enabled (e.g., opened or re-focused)
    private void OnEnable()
    {
        LoadFavorites(); // Load favorites when the window is enabled
    }

    private void OnGUI() {
        // 顶部工具栏：清空和统计
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(20));

        // 清空按钮：带二次确认弹窗
        if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(50))) {
            if (favoriteAssetPaths.Count > 0) {
                if (EditorUtility.DisplayDialog("清空收藏夹",
                    $"确定要移除全部 {favoriteAssetPaths.Count} 个收藏项吗？", "确定", "取消")) {
                    favoriteAssetPaths.Clear();
                    SaveFavorites();
                    GUIUtility.ExitGUI();
                }
            }
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label($"数量: {favoriteAssetPaths.Count}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        // 滚动列表区域
        scrollPos = GUILayout.BeginScrollView(scrollPos);

        for (int i = 0; i < favoriteAssetPaths.Count; i++) {
            string assetPath = favoriteAssetPaths[i];
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

            // 资产丢失处理：如果项目被删除，依然显示路径并允许手动剔除
            if (asset == null) {
                DrawMissingRow(assetPath, i);
                continue;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // 绘制资产图标
            Texture2D icon = AssetPreview.GetMiniThumbnail(asset);
            GUILayout.Label(new GUIContent(icon), GUILayout.Width(20), GUILayout.Height(16));

            // 高亮选中的资产背景
            Color defaultColor = GUI.color;
            if (lastClickedAsset == asset) GUI.color = Color.yellow;

            // 资产名称按钮：支持左键跳转，右键菜单
            if (GUILayout.Button(new GUIContent(asset.name, assetPath), EditorStyles.label, GUILayout.ExpandWidth(true))) {
                Event e = Event.current;
                if (e.button == 0) // 左键
                {
                    HandleAssetClick(asset);
                } else if (e.button == 1) // 右键
                  {
                    contextMenuAsset = asset;
                    ShowContextMenu();
                    e.Use();
                }
            }

            GUI.color = defaultColor; // 恢复颜色

            // 单项移除按钮 (x)
            if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(18))) {
                favoriteAssetPaths.RemoveAt(i);
                SaveFavorites();
                // 立即退出本次GUI，防止后续循环索引越界报错
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        // 处理外部资源拖入面板的行为 (保留最基础的拖入收藏功能)
        HandleDragAndDrop();
    }

    // 处理列表中已丢失资产的特殊显示
    private void DrawMissingRow(string path, int index) {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        GUI.color = Color.gray;
        GUILayout.Label("[丢失/已移动]", GUILayout.Width(80));
        GUILayout.Label(path, EditorStyles.miniLabel);
        GUI.color = Color.white;

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("×", GUILayout.Width(20))) {
            favoriteAssetPaths.RemoveAt(index);
            SaveFavorites();
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void HandleDragAndDrop()
    {
        Event e = Event.current;
        if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (Object obj in DragAndDrop.objectReferences)
                {
                    string path = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(path) && !favoriteAssetPaths.Contains(path))
                        favoriteAssetPaths.Add(path);
                }
                SaveFavorites(); // Save after adding
                e.Use();
            }
        }
    }

    private void ShowContextMenu()
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Remove"), false, RemoveSelectedAsset);
        menu.AddItem(new GUIContent("Open in Explorer"), false, OpenInExplorer);
        menu.ShowAsContext();
    }

    private void RemoveSelectedAsset()
    {
        if (contextMenuAsset != null)
        {
            string path = AssetDatabase.GetAssetPath(contextMenuAsset);
            favoriteAssetPaths.Remove(path);
            contextMenuAsset = null;
            SaveFavorites(); // Save after removal
            Repaint(); // Refresh UI
        }
    }

    private void OpenInExplorer()
    {
        if (contextMenuAsset != null)
        {
            EditorUtility.RevealInFinder(AssetDatabase.GetAssetPath(contextMenuAsset));
        }
    }

    private void HandleAssetClick(Object asset)
    {
        float timeSinceLastClick = Time.realtimeSinceStartup - lastClickTime;
        lastClickTime = Time.realtimeSinceStartup;

        lastClickedAsset = asset; // Highlight last clicked asset

        if (timeSinceLastClick < doubleClickTime)
        {
            if (asset is SceneAsset)
            {
                if (EditorApplication.isPlaying)
                {

                    // 保存场景引用
                    SceneAsset sceneToLoad = asset as SceneAsset;
                    string scenePath = AssetDatabase.GetAssetPath(sceneToLoad);

                    bool shouldStop = EditorUtility.DisplayDialog(
                        "#资产收藏夹",
                        $"确定要退出播放模式并加载：\n" +
                        $" {sceneToLoad.name} ？\n\n",
                        "确认",
                        "取消"
                    );

                    if (shouldStop) {
                        EditorApplication.isPlaying = false;

                        EditorApplication.CallbackFunction checkPlayMode = null;
                        checkPlayMode = () =>
                        {
                            if (!EditorApplication.isPlaying) {
                                EditorApplication.update -= checkPlayMode;

                                EditorApplication.delayCall += () =>
                                {
                                    if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
                                    }
                                };
                            }
                        };

                        EditorApplication.update += checkPlayMode;
                    }
                }
                else
                {
                    // Open scene normally
                    string scenePath = AssetDatabase.GetAssetPath(asset);
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
                }
            }
            else
            {
                AssetDatabase.OpenAsset(asset);
            }

            // Reset last click time
            lastClickTime = 0;
        }
        else
        {
            // Single click → Focus on asset
            EditorGUIUtility.PingObject(asset);
        }

        Repaint(); // Refresh UI to update highlight
    }

    private void SaveFavorites()
    {
        EditorPrefs.SetString(PREF_KEY, string.Join(";", favoriteAssetPaths));
    }

    private void LoadFavorites()
    {
        if (EditorPrefs.HasKey(PREF_KEY))
        {
            string savedData = EditorPrefs.GetString(PREF_KEY);
            favoriteAssetPaths = savedData.Split(';')
                .Where(s => !string.IsNullOrEmpty(s)) // Filter out empty entries
                .ToList();
        }
        else
        {
            favoriteAssetPaths = new List<string>(); // Ensure list is initialized
        }
    }
}
