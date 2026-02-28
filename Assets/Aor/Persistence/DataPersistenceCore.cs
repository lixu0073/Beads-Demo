using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aor.Persistence
{
    public interface IDataPersistenceIgnore { }
    public interface IDataPersistence
    {
        string GetStorageKey(); // 获取用来存储该component的key，一般是组件的名字。
        void OnSave(PersistenceInfo info);
        void OnLoad(PersistenceInfo info);
    }

    public delegate string PrefabNameRetriever(Transform game_object);
    public delegate UniTask<TResult> PrefabSpawner<TData, TResult>(TData customData, string prefab_name, TopGameObjectInfo info);
    public delegate GameObject SyncPrefabSpawner(string prefab_name);
    public delegate GameObject ItemSpawner(string prefab_name, TopUIObjectInfo info);

    public class CustomSettings<TData, TResult>
    {
        // 函数， 返回prefab的name。这个name在下次载入时，被用来获取prefab。
        public PrefabNameRetriever prefab_name_retriever;
        // 函数，根据Prefab的name，创建新的item。
        public ItemSpawner item_prefab_spawner;
        // 函数，根据Prefab的name，创建新的object。
        public PrefabSpawner<TData, TResult> object_prefab_spawner;
    }
    /*
     * 持久化Transform的所有子GameObject。
     */
    public class DataPersistence<TData, TResult>
    {
        private CustomSettings<TData, TResult> settings;

        public DataPersistence(CustomSettings<TData, TResult> settings) {
            this.settings = settings;
        }
        /*
         * 保存root层级下的所有game objects。
         */
        public bool Save2(Transform root, string fileFullPath) {
            List<TopGameObjectInfo> objects_info = new List<TopGameObjectInfo>();

            for (int i_child = 0; i_child < root.childCount; ++i_child) {
                var child = root.GetChild(i_child);
                var game_object_info = new TopGameObjectInfo();

                // save children info
                if (child.childCount > 0) {
                    SaveRecursively(child, game_object_info);
                }

                // save prefab_name info
                game_object_info.prefab_name = this.settings.prefab_name_retriever(child);

                objects_info.Add(game_object_info);
            }

            FileUtil.Serialize(fileFullPath, objects_info, false);
            return true;
        }
        /*
         * 保存一个UI的信息。
         */
        public GameObject LoadUIFromData(TopUIObjectInfo storage) {
            if (storage == null) {
                Debug.LogError("invalid top ui object info");
                return null;
            }
            if (this.settings.item_prefab_spawner == null) {
                Debug.LogError("invalid item_prefab_spawner");
                return null;
            }
            var item = this.settings.item_prefab_spawner(storage.prefab_name, storage);
            if (item == null) {
                Debug.LogError("item prefab not found:" + storage.prefab_name);
                return null;
            }
            return item;
        }
        public GameObject LoadDataToGUI(TopUIObjectInfo item, GameObject item_game_object) {
            if (item == null || item_game_object == null) {
                Debug.LogError("invalid top ui object info");
                return null;
            }

            var coms = item_game_object.GetComponents<IDataPersistence>();
            foreach (var com in coms) {
                PersistenceInfo data;
                if (item.components_info.TryGetValue(com.GetStorageKey(), out data)) {
                    com.OnLoad(data);
                }
            }

            return item_game_object;
        }
        public TopUIObjectInfo SaveUITo(TopUIObjectInfo target, string prefab_id, GameObject game_object, bool process_children = false) {
            // save children info
            SaveUIGameObject(game_object.transform, target, process_children);

            // save prefab_name info
            target.prefab_name = this.settings.prefab_name_retriever(game_object.transform);
            return target;
        }
        // 如果children上也有 IDataPersistence 组件，则需要设置 process_children=true
        public TopUIObjectInfo SaveUI(string prefab_id, GameObject game_object, bool process_children = false) {
            return this.SaveUITo(new TopUIObjectInfo(), prefab_id, game_object, process_children: process_children);
        }
        bool SaveUIGameObject(Transform root, UIObjectInfo game_object_info, bool process_children) {
            // save IDataPersistence components info
            var coms = root.GetComponents<IDataPersistence>();
            foreach (var com in coms) {
                var key = com.GetStorageKey();
                if (game_object_info.components_info.TryGetValue(key, out var pinfo) == false) {
                    pinfo = new PersistenceInfo();
                    game_object_info.components_info[key] = pinfo;
                }
                com.OnSave(pinfo);
            }

            // save recursively
            if (process_children) {
                for (int i_child = 0; i_child < root.childCount; i_child++) {
                    var child = root.GetChild(i_child);
                    if (child.TryGetComponent<DataPersistenceIgnore>(out var _)) {
                        continue;
                    }
                    var child_info = new UIObjectInfo();
                    SaveUIGameObject(child, child_info, true);
                    game_object_info.children_info.Add(child_info);
                }
            }
            return true;
        }
        /*
         * 保存一个game object的信息。
         */
        public TopGameObjectInfo Save(string prefab_id, GameObject game_object, bool recursively = true, TopGameObjectInfo game_object_info = null) {
            if (game_object_info == null) {
                game_object_info = new TopGameObjectInfo();
            }

            // save children info
            SaveRecursively(game_object.transform, game_object_info, recursively);

            // save prefab_name info
            if (this.settings.prefab_name_retriever == null) {
                Debug.LogError("prefab_name_retriever fn isn't set");
                return null;
            }
            if (string.IsNullOrEmpty(prefab_id)) {
                game_object_info.prefab_name = this.settings.prefab_name_retriever(game_object.transform);
            } else {
                game_object_info.prefab_name = prefab_id;
            }
            return game_object_info;
        }
        public bool SaveToFile2(object info, string fullPath) {
            try {
                FileUtil.Serialize(fullPath, info, false);
            } catch (Exception) {
                return false;
            }
            return true;
        }
        bool SaveRecursively(Transform root, GameObjectInfo game_object_info, bool recursively = true) {
            // save IDataPersistence components info
            var coms = root.GetComponents<IDataPersistence>();
            foreach (var com in coms) {
                var pinfo = new PersistenceInfo();
                try {
                    com.OnSave(pinfo);
                } catch (Exception e) {
                    Debug.LogException(e);
                    Debug.LogError("failed to save component: " + com.GetStorageKey() + " exception:" + e.ToString());
                    continue;
                }
                var key = com.GetStorageKey();
                if (game_object_info.components_info.ContainsKey(key)) {
                    Debug.LogError("duplicated component key: `" + key + "`, com:" + com.GetType().ToString() + ", obj-name:" + root.name);
                    foreach (var c in root.GetComponents<Component>()) {
                        var dp = c as IDataPersistence;
                        if (dp != null) {
                            Debug.Log("com.key:" + dp.GetStorageKey() + " com.name:" + c);
                        }
                    }
                    continue;
                }
                game_object_info.components_info[key] = pinfo;
            }

            // save transform component's info
            game_object_info.transform.SaveFrom(root);

            if (!recursively) return true;

            // save recursively
            for (int i_child = 0; i_child < root.childCount; ++i_child) {
                var child = root.GetChild(i_child);
                if (child.GetComponent<IDataPersistenceIgnore>() != null) {
                    continue;
                }
                var child_info = new GameObjectInfo();
                SaveRecursively(child, child_info);

                game_object_info.children_info.Add(child_info);
            }
            return true;
        }
        /*
         * 载入文件内容并挂到root层级下。
         */
        public async UniTask<List<TopGameObjectInfo>> LoadFromObjectsInfo(TData customData, List<TopGameObjectInfo> objects_info, System.Action<TResult> each_game_object_fn = null) {
            for (int i = 0; i < objects_info.Count; i++) {
                var object_info = objects_info[i];
                object_info.BelongingList = objects_info;
                object_info.IndexInList = i;
                var game_object = await LoadFromObjectInfo(customData, object_info, false);
                if (game_object == null) continue; // 直接忽略错误，处理object_prefab已经不存在了的情况。
                if (each_game_object_fn != null) {
                    each_game_object_fn(game_object);
                }
            }
            return objects_info;
        }
        public async UniTask<TResult> LoadFromObjectInfo(TData customData, TopGameObjectInfo object_info, bool process_children) {
            if (object_info == null) {
                return default(TResult);
            }
            var game_object = await this.settings.object_prefab_spawner(customData, object_info.prefab_name, object_info);
            if (game_object == null) {
                Debug.LogError($"prefab-id not found:{object_info.prefab_name}");
                return default(TResult); // 直接忽略错误
            }
            // LoadRecursively(game_object.transform, object_info, process_children);
            return game_object;
        }
        bool LoadRecursively(Transform root, GameObjectInfo game_object_info, bool process_children) {
            game_object_info.LoadToObject(root);

            // load recursively
            if (process_children && root.childCount > 0) // 有些gameObject会动态创建子物体。在刚刚Instantiate之后，子物体是不存在的。
            {
                for (int i_child = 0; i_child < game_object_info.children_info.Count; ++i_child) {
                    var child_info = game_object_info.children_info[i_child];
                    if (i_child >= root.childCount) {
                        Debug.LogError("failed to load: " + root.name);
                        continue;
                    }
                    var child = root.GetChild(i_child);
                    LoadRecursively(child, child_info, true);
                }
            }
            return true;
        }

        public void LoadUIFromObjectsInfo(Transform root, List<TopUIObjectInfo> objects_info, System.Action<GameObject> each_game_object_fn = null) {
            foreach (var object_info in objects_info) {
                var game_object = this.settings.item_prefab_spawner(object_info.prefab_name, object_info);
                //                LoadUIRecursively(game_object.transform, object_info);
                game_object.transform.SetParent(root, false);
                each_game_object_fn?.Invoke(game_object);
            }
        }
        bool LoadUIRecursively(Transform root, UIObjectInfo game_object_info) {
            // load IDataPersistence components info
            var coms = root.GetComponents<IDataPersistence>();
            foreach (var com in coms) {
                PersistenceInfo data;
                if (game_object_info.components_info.TryGetValue(com.GetStorageKey(), out data)) {
                    com.OnLoad(data);
                }
            }

            // load recursively
            for (int i_child = 0; i_child < game_object_info.children_info.Count; ++i_child) {
                var child_info = game_object_info.children_info[i_child];
                var child = root.GetChild(i_child);
                LoadUIRecursively(child, child_info);
            }
            return true;
        }
    }

}



