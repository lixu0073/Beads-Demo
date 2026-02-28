using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class GameObjectUtil
{
    public static Transform GetOutermostParent(Transform current)
    {
        if (current == null) return null;
        var parent = current.parent;
        if (parent == null) return current;
        return GetOutermostParent(parent);
    }
    // 获取game object的路径
    public static string GetPath(Transform current)
    {
        if (current == null) return "[Root:null]";
        var sceneName = current.gameObject.scene.name;
        var sb = new StringBuilder();
        while (current != null)
        {
            sb.Append(current.name);
            sb.Append("<-");
            current = current.parent;
        }
        sb.Append($"[scene:{sceneName}]");
        return sb.ToString();
    }
    // 获取直接孩子节点上挂载的组件。无论孩子节点是否active。
    public static List<ComType> GetDirectChildrenComponents<ComType>(Transform root) where ComType : class
    {
        var res = new List<ComType>();
        for (int i = 0; i < root.childCount; i++)
        {
            foreach (var com in root.GetChild(i).GetComponents<ComType>())
            {
                res.Add(com);
            }
        }
        return res;
    }
    public static void SetChildrenLayer(Transform parent, int layer)
    {
        parent.gameObject.layer = layer;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            child.gameObject.layer = layer;
            SetChildrenLayer(child, layer);
        }
    }
    public static void SetChildrenLayerWithNoOverwrite(Transform parent, int layer, int target_layer)
    {
        parent.gameObject.layer = layer;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.gameObject.layer == target_layer)
            {
                child.gameObject.layer = layer;
                SetChildrenLayer(child, layer);
            }
        }
    }
    public static void SetStatic(Transform parent, bool is_static)
    {
        parent.gameObject.isStatic = is_static;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            child.gameObject.isStatic = is_static;
            if (child.childCount > 0)
            {
                SetStatic(child, is_static);
            }
        }
    }
    // object专用逻辑
    public static void SetObjectStatic(Transform parent, bool is_static)
    {
        parent.gameObject.isStatic = is_static;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == "icon") continue;
            child.gameObject.isStatic = is_static;
            if (child.childCount > 0)
            {
                SetObjectStatic(child, is_static);
            }
        }
    }
    public static Transform FindChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }
            child = FindChild(child, name);
            if (child != null)
            {
                return child;
            }
        }
        return null;
    }
    public static void ClearChildren(Transform root, int start_index = 0, bool immediate = false)
    {
        while (root.childCount > start_index)
        {
            var child = root.GetChild(start_index);
            if (immediate)
            {
                GameObject.DestroyImmediate(child.gameObject);
            } else
            {
                child.SetParent(null);
                GameObject.Destroy(child.gameObject);
            }
        }
    }
    public static void SetPSEnabled(Transform parent, bool enabled, bool sound_loop)
    {
        // 处理声音的淡入淡出
        var sound = parent.GetComponent<AudioSource>();
        if (sound != null)
        {
            if (enabled)
            {
                // PSUtil.RandomizeAudio(sound);
                sound.loop = true;
                sound.Play();
            } else
            {
                sound.loop = false;
            }
        }

        // 处理ps的启停
        var ps = parent.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            if (enabled)
            {
                ps.Play(true);
            } else
            {
                ps.Stop(true);
            }
        } else
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                SetPSEnabled(parent.GetChild(i), enabled, sound_loop);
            }
        }
    }
    // 遍历每个孩子节点（可以包含跟节点）。处理过程中不可进行 reparent 操作。
    public static void EachChildren(Transform parent, System.Action<Transform> cb, bool contains_root = false)
    {
        if (contains_root)
        {
            cb(parent);
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            cb(child);
            EachChildren(child, cb, false);
        }
    }
    // 按相反的顺序进行遍历
    public static void EachChildren_Reverse(Transform parent, System.Action<Transform> cb, bool contains_root = false)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            EachChildren_Reverse(child, cb, false);
            cb(child);
        }
        if (contains_root)
        {
            cb(parent);
        }
    }
    // 遍历每个孩子节点（可以包含根节点）
    // cb返回true，表示继续遍历其children；否则，不再遍历其children。
    public static void EachChildren(Transform parent, System.Predicate<Transform> cb, bool contains_root = false)
    {
        if (contains_root)
        {
            if (!cb(parent))
            {
                return;
            }
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (cb(child))
            {
                EachChildren(child, cb, false);
            }
        }
    }
    // 遍历每个孩子节点（可以包含跟节点）
    public static bool FindFirst(Transform parent, bool consider_root, System.Predicate<Transform> cb)
    {
        if (consider_root)
        {
            if (cb(parent)) return true;
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (cb(child)) return true;
            if (FindFirst(child, false, cb))
            {
                return true;
            }
        }
        return false;
    }
    public static bool IsParent(Transform child, Transform parent)
    {
        var p = child.parent;
        while (p != null)
        {
            if (p == parent)
            {
                return true;
            }
            p = p.parent;
        }
        return false;
    }
    // 将 [a-z] 开头的子物体，加入 result 中。
    // only_first_level=true 表示搜索到第一个满足添加的物体之后，其子物体不会被继续搜索。
    public static void ChildToMap(Transform parent, Dictionary<string, Transform> result, bool only_first_level)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name.StartsWith("_"))
            {
                continue;
            }
            var firstLetter = child.name[0];
            if (firstLetter <= 'z' && firstLetter >= 'a')
            {
                if (result.ContainsKey(child.name))
                {
                    Debug.LogError("duplicated name:" + child.name + "\npath:" + GetPath(child) +
                        "\nlast_path:" + GetPath(result[child.name]));
                } else
                {
                    result[child.name] = child;
                }
                if (only_first_level)
                {
                    continue;
                }
            }
            ChildToMap(child, result, only_first_level);
        }
    }
    public static Dictionary<string, Transform> FindChildren(Transform parent, params string[] names)
    {
        var res = new Dictionary<string, Transform>();
        HashSet<string> names_set = null;
        if (names != null && names.Length > 0)
        {
            names_set = new HashSet<string>(names);
        }
        InnerFindChildren(parent, names_set, res);
        return res;
    }
    private static void InnerFindChildren(Transform parent, HashSet<string> names,
        Dictionary<string, Transform> result)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (names != null)
            {
                foreach (var name in names)
                {
                    if (child.name == name)
                    {
                        result[name] = child;
                        break;
                    }
                }
            } else if (child.name[0] >= 'a' && child.name[0] <= 'z')
            {
                result[child.name] = child;
            }
            InnerFindChildren(child, names, result);
        }
    }
    public static Transform FindChildWithPrefix(Transform parent, string prefix)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name.StartsWith(prefix))
            {
                return child;
            }
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            var res = FindChildWithPrefix(child, prefix);
            if (res != null) return res;
        }
        return null;
    }
    public static Dictionary<string, Transform> FindChildrenWithPrefix(Transform parent, string prefix)
    {
        var res = new Dictionary<string, Transform>();
        InnerFindChildrenWithPrefix(parent, prefix, res);
        return res;
    }
    private static void InnerFindChildrenWithPrefix(Transform parent, string prefix,
        Dictionary<string, Transform> result)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name.StartsWith(prefix))
            {
                result[child.name] = child;
            }
            InnerFindChildrenWithPrefix(child, prefix, result);
        }
    }
}
