using Aor.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Window Sort Config", menuName = "AorConfig/GUI/Window Sort Config")]
public class WindowsSortAsset : ScriptableObject
{
    [SerializeField] string debugTip;
    [Tooltip("越往下，越靠后渲染。")]
    [SerializeField] public BaseWindow[] windows;
    [SerializeField] public bool CancelToClose = true; // 通过UICancel快捷键关闭界面
}
