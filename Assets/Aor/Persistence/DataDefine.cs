using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Aor.Persistence
{
    [Serializable]
    public class PersistenceInfo
    {
        public Dictionary<string, object> info = new Dictionary<string, object>();
        public int GetCount() {
            return info.Count;
        }
        public override string ToString() {
            string res = "[PInfo:";
            foreach (var pair in info) {
                res += "(" + pair.Key + "," + pair.Value + "),";
            }
            return res;
        }
        public void Set(string key, object value) {
            this.info[key] = value;
        }
        public void MergeFrom(PersistenceInfo info) {
            foreach (var p in info.info) {
                this.info[p.Key] = p.Value;
            }
        }
        //索引
        public virtual object this[string index] {
            get {
                return info[index];
            }
            set {
                info[index] = value;
            }
        }

        // for convenience
        public bool ContainsKey(string key) {
            return info.ContainsKey(key);
        }

        // for convenience
        public T GetObject<T>(string key) where T : class {
            object value;
            if (info.TryGetValue(key, out value)) {
                if (value == null) {
                    return default(T);
                }
                try {
                    if (value.GetType() == typeof(float)) {
                        return (T)value;
                    }
                    if (value.GetType() == typeof(int)) {
                        return (T)value;
                    }
                    if (value.GetType() == typeof(System.Int64)) {
                        Debug.LogError("invalid int64:" + value);
                        return default(T);
                    }
                    return (T)value;
                } catch (Exception e) {
                    Debug.LogException(e);
                    if (value != null) {
                        Debug.LogError("type:" + value.GetType());
                    }
                    return default(T);
                }
            }
            return default(T);
        }
        public bool Bool(string key) {
            object value;
            if (info.TryGetValue(key, out value)) {
                return (bool)value;
            }
            return false;
        }
        public string Str(string key) {
            object value;
            if (info.TryGetValue(key, out value)) {
                return (string)value;
            }
            return "";
        }
        public int Int(string key) {
            object value;
            if (info.TryGetValue(key, out value)) {
                var t = value.GetType();
                if (t == typeof(System.Int64)) {
                    return (int)(System.Int64)value;
                } else if (t == typeof(System.Double)) {
                    Debug.LogError("trying to get a double value, key:" + key);
                    return (int)(System.Double)value;
                } else if (t == typeof(int)) {
                    return (int)value;
                } else if (t == typeof(float)) {
                    Debug.LogError("trying to get a float value, key:" + key);
                    return (int)(float)value;
                } else {
                    return (int)value;  // 枚举值
                }
            }
            return 0;
        }
        public float Float(string key) {
            object value;
            if (info.TryGetValue(key, out value)) {
                var t = value.GetType();
                if (t == typeof(System.Int64)) {
                    Debug.LogError("trying to get a int64 value, key:" + key);
                    return (float)(System.Int64)value;
                } else if (t == typeof(System.Double)) {
                    return (float)(System.Double)value;
                } else if (t == typeof(int)) {
                    Debug.LogError("trying to get a int value, key:" + key);
                    return (float)(int)value;
                } else if (t == typeof(float)) {
                    return (float)value;
                } else {
                    Debug.LogError("unsupported value for target type `int`:" + t);
                }
            }
            return 0;
        }
        public bool Float(string key, ref float result) {
            object value;
            if (info.TryGetValue(key, out value)) {
                var t = value.GetType();
                if (t == typeof(System.Int64)) {
                    Debug.LogError("trying to get a int64 value, key:" + key);
                    result = (float)(System.Int64)value;
                } else if (t == typeof(System.Double)) {
                    result = (float)(System.Double)value;
                } else if (t == typeof(int)) {
                    Debug.LogError("trying to get a int value, key:" + key);
                    result = (float)(int)value;
                } else if (t == typeof(float)) {
                    result = (float)value;
                } else {
                    Debug.LogError("unsupported value for target type `int`:" + t);
                    return false;
                }
                return true;
            } else {
                return false;
            }
        }
    }
    [Serializable]
    public class BasicGameObjectInfo
    {
        public Dictionary<string, PersistenceInfo> components_info = new Dictionary<string, PersistenceInfo>();
        public virtual void Reset() {
            this.components_info.Clear();
        }
    }
    [Serializable]
    public class GameObjectInfo :BasicGameObjectInfo
    {
        public TransformSerializable transform = new TransformSerializable();
        [JsonIgnore]  // 暂时不需要该字段
        public List<GameObjectInfo> children_info = new List<GameObjectInfo>();
        public void LoadToObject(Transform root, bool apply_transform = true) {
            // load transform component's info
            if (apply_transform) {
                this.transform.LoadTo(root);
            }

            // load IDataPersistence components info
            var coms = root.GetComponents<IDataPersistence>();
            foreach (var com in coms) {
                PersistenceInfo data;
                if (this.components_info.TryGetValue(com.GetStorageKey(), out data)) {
                    if (data == null) {
                        Debug.LogError("invalid data for component:" + com.GetStorageKey());
                        continue;
                    }
                    com.OnLoad(data);
                }
            }
        }
        public override void Reset() {
            base.Reset();
            this.children_info.Clear();
        }
    }
    [Serializable]
    public class UIObjectInfo :BasicGameObjectInfo
    {
        public List<UIObjectInfo> children_info = new List<UIObjectInfo>();
        public int slot_index = 0;
    }

    [Serializable]
    public class TopUIObjectInfo :UIObjectInfo
    {
        public string prefab_name;
    }
    public enum EntityStorageStatus
    {
        Unload = 1, Loaded = 2,
        Deleted = 4,
    }
    [Serializable]
    public class TopGameObjectInfo :GameObjectInfo
    {
        public string prefab_name;


        [System.NonSerialized]
        [JsonIgnore]
        public object ReferenceEntity;  // 引用ObjectCommon，用来判断是否已经加载了。
        [System.NonSerialized]
        [JsonIgnore]
        public List<TopGameObjectInfo> BelongingList;  // 用于debug
        [System.NonSerialized]
        [JsonIgnore]
        public int IndexInList = -1;  // 用来快速从列表中移除此数据。相当于key。
        [System.NonSerialized]
        [JsonIgnore]
        public bool Persistent;  // 用于debug


        public void AddToList(List<TopGameObjectInfo> list) {
#if DEBUG
            if (this.Persistent == false) {
                Debug.LogError("bug");
                return;
            }
            if (this.BelongingList != null) {
                Debug.LogError("??");
            }
#endif
            this.BelongingList = list;
            this.IndexInList = list.Count;
            list.Add(this);
        }
        [System.Diagnostics.Conditional("DEBUG")]
        public void ValidateList(List<TopGameObjectInfo> list) {
#if DEBUG
            if (this.BelongingList != list) {
                Debug.LogError("??:" + this.BelongingList + ", list:" + list);
            }
#endif
        }

        public void RemoveFromList(List<TopGameObjectInfo> __list) {
            if (__list != null) ValidateList(__list);
            this.RemoveFromList();
        }

        public void RemoveFromList() {
            var l = this.BelongingList;
#if DEBUG
            if (IndexInList < 0) Debug.LogError("??");
            if (this.BelongingList == null) Debug.LogError("??");
            if (l[IndexInList] != this) Debug.LogError("??");
#endif
            if (IndexInList < l.Count - 1) {
                l[IndexInList] = l[l.Count - 1];
                l[IndexInList].IndexInList = IndexInList;
            }
            l.RemoveAt(l.Count - 1);

            this.IndexInList = -1;
            this.BelongingList = null;
        }
        public int Index {
            get {
                return this.IndexInList;
            }
        }
        public bool InStorage {
            get {
                return this.BelongingList != null;
            }
        }


        [System.Diagnostics.Conditional("DEBUG")]
        public void Validate() {
            if (this.BelongingList != null) {
                if (this.IndexInList < 0) {
                    Debug.LogError("??:" + this.IndexInList);
                    return;
                }
                if (this.IndexInList >= this.BelongingList.Count) {
                    Debug.LogError("??");
                    return;
                }
                if (this.BelongingList[this.IndexInList] != this) {
                    Debug.LogError("??");
                    return;
                }
            }
        }
    }
}



