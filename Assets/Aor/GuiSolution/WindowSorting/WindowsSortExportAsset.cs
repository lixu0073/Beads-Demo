using System.Collections.Generic;
using Aor.UI;
using UnityEngine;

[System.Serializable]
public struct WinOrder
{
    public string windowType;
    public int order;
}

[CreateAssetMenu(fileName = "Window Sort Export Config", menuName = "AorConfig/GUI/Window Sort Export Config")]
public class WindowsSortExportAsset : ScriptableObject
{
    [SerializeField] string debugTip;  // 随便写点啥，方便开发者记忆此配置的功能
    [Tooltip("越往下则越靠前，越靠后渲染。")]
    [SerializeField] public WinOrder[] orders;

    public void UpdateOrders(List<BaseWindow> sortedWindows)
    {
        HashSet<BaseWindow> windows = new HashSet<BaseWindow>();
        foreach (var win in sortedWindows)
        {
            if (win.GetType().Name != win.name)
            {
                Debug.LogError("view名称和view脚本的名称不匹配:" + win.name + ".prefab,  script-type:" + win.GetType().Name);
            }
            if (windows.Add(win) == false)
            {
                Debug.LogError("window重复配置order:" + win);
            }
        }

        this.orders = new WinOrder[sortedWindows.Count];
        for (int i = 0; i < sortedWindows.Count; i++)
        {
            this.orders[i] = new WinOrder() {
                order = i,
                windowType = sortedWindows[i].GetType().Name,
            };
        }
        EditorUtil.SetDirty(this);
    }
}
