using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Aor.UI.WindowManagement
{
    public class WindowOrderConfig : MonoBehaviour
    {
        [SerializeField] WindowsSortAsset[] sorts;
        [SerializeField] int baseOrder = 1;
        [SerializeField] WindowsSortExportAsset exportAsset;
        [SerializeField] UnityEngine.Object windowsDirectory;

        [Header("查找窗口")]
        [SerializeField] BaseWindow[] toFindWindows;


#if UNITY_EDITOR
        [EditorCools.Button("==> 刷新界面的渲染顺序 <==")]
        void SetupOrder()
        {
            var wins = this.GenAllWindowsSorting();

            // 检查是否所有窗口都有配置
            {
                var dirPath = AssetDatabase.GetAssetPath(this.windowsDirectory);
                var windowGUIDs = AssetDatabase.FindAssets("t:prefab", new string[] { dirPath });

                var allWindowNames = new HashSet<string>();
                foreach (var win in wins)
                {
                    allWindowNames.Add(win.name);
                }
                foreach (var prefabGUID in windowGUIDs)
                {
                    var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGUID);
                    var prefabName = FileUtil.GetFileNameWithoutExtension(prefabPath);
                    if (allWindowNames.Contains(prefabName) == false)
                    {
                        Debug.LogError("window wasn't configed, path:" + prefabPath);
                    }
                }
            }


            this.exportAsset.UpdateOrders(wins);
            UnityEditor.AssetDatabase.SaveAssets();
        }

        // 1.检查有没有手动配置的baseOrder 2.所有animator设置为unscaled-time 3.新增到all windows的prefab，自动加入最上层。
        [EditorCools.Button("自动修正界面的配置")]
        void SetupWins()
        {
            this.SyncConf();
            this.BatchSetConfig();
        }
        [EditorCools.Button("[调试] 查找窗口")]
        void SetupOrder2()
        {
            var wins = new List<BaseWindow>();
            foreach (var s in this.sorts)
            {
                int winIndex = 0;
                foreach (var win in s.windows)
                {
                    foreach (var targetWin in this.toFindWindows)
                    {
                        if (win == targetWin)
                        {
                            Debug.Log($"window found at order `{s.name}[{winIndex}]`");
                        }
                    }
                    winIndex++;
                }
            }
        }

        List<BaseWindow> GenAllWindowsSorting()
        {
            var wins = new List<BaseWindow>();
            foreach (var s in this.sorts)
            {
                foreach (var win in s.windows)
                {
                    if (win == null)
                    {
                        Debug.LogError("window is null in config:" + s.name);
                        continue;
                    }
                    wins.Add(win);
                }
            }
            return wins;
        }
        void SyncConf()
        {
            foreach (var s in this.sorts)
            {
                foreach (var win in s.windows)
                {
                    if (win.CancelToClose != s.CancelToClose)
                    {
                        win.CancelToClose = s.CancelToClose;
                        EditorUtil.SetDirty(win);
                    }
                }
            }
        }
        void BatchSetConfig()
        {
            foreach (var s in this.sorts)
            {
                foreach (var w in s.windows)
                {
                    var anim = w.GetComponent<Animator>();
                    if (anim != null && anim.updateMode != AnimatorUpdateMode.UnscaledTime)
                    {
                        anim.updateMode = AnimatorUpdateMode.UnscaledTime;
                        EditorUtil.SetDirty(w);
                    }
                }
            }
        }
#endif
    }
}