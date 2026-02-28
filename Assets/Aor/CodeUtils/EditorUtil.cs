using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorApplication;

public static class EditorUtil
{
    public static bool IsPlaying {
        get {
#if UNITY_EDITOR
            return EditorApplication.isPlaying;
#else
                return true;
#endif
        }
    }
    // 在editor中且不在playing状态
    public static bool IsEditing {
        get {
            return IsEditor && !IsPlaying;
        }
    }
    // 是否是编译出的独立版本。
    public static bool IsBuild {
        get {
#if UNITY_EDITOR
            return false;
#else
                return true;
#endif
        }
    }
    public static bool IsEditor {
        get {
#if UNITY_EDITOR
            return true;
#else
                return false;
#endif
        }
    }
    public static bool IsDebug {
        get {
#if DEBUG
            return true;
#else
                return false;
#endif
        }
    }
    public static bool IsPublish {
        get {
#if DEBUG || UNITY_EDITOR
            return false;
#else
                return true;
#endif
        }
    }

    [System.Diagnostics.Conditional("DEBUG")]
    public static void SetDirty(UnityEngine.Object target)
    {
#if UNITY_EDITOR
        if (isPlaying) return;
        UnityEditor.EditorUtility.SetDirty(target);
#endif
    }

    public static string GetAssetPath(UnityEngine.Object target)
    {
#if UNITY_EDITOR
        return AssetDatabase.GetAssetPath(target);
#else
            return "NotAvailable";
#endif
    }
}
