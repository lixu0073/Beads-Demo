using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Aor.UI
{
    public enum LayerType
    {
        None,  // 默认值，异常。
        SceneUI,   // 跟随场景的UI
        MainUI, // 主界面
        MutexWindow, // 装备页
        MultiWindow,  // 复杂场景，需要同时展示多个页面
        FloatingPanel, // 物品详情等悬浮窗口。其他窗口关闭时会自动关闭此窗口
        ModelWindow,   // message-box，必须要玩家点击才能关闭。并且同时不能点击其他UI。
        Notice,  // 通知、toast等，显示在最上方
        Additive,  // 多个互斥的窗口需要同时显示，但是只有一个是focus状态。
        IndependentView,  // 独立窗口，代码中去显式控制开启和关闭，不受其他layer影响。

        // 这里可以加新的类型。因为已有GameObject里面已经设置了LayerType，所以不能修改上面的顺序。
        // Layer的顺序是通过添加顺序来决定的，所以这里只需要append新的Layer即可。

        // 自动计数
        LayerTypeCount,
    }

    public class Layer
    {
        public LayerType type;
        public bool AutoClear = true;  // 当底层其他layer变化的时候，是否自动清空当前layer。为true一般是重要的主window所在的layer。
        public bool IsMutex = false;  // 同一层是否只能展示一个窗口
        public List<BaseWindow> rootWindows = new List<BaseWindow>();  // 不包含sub-windows
        public LayerType[] layersToClear;  // 当该layer有window被Open的时候，自动清空另外的layer。

        public void ClearWindows(WindowManager winMgr = null, bool deleteInstance = false)
        {
            for (int i = this.rootWindows.Count - 1; i >= 0; i--)
            {
                var win = this.rootWindows[i];
                try
                {
                    win.CloseChildren(deleteInstance: deleteInstance);
                    win._CloseWindowByMgr();
                } catch (System.Exception e)
                {
                    Debug.LogException(e);
                    Debug.LogError("failed to close win:" + win);
                }
            }
            if (deleteInstance)
            {
                foreach (var win in this.rootWindows)
                {
                    try
                    {
                        winMgr.DestroyWindowInstance(win);
                    } catch (System.Exception e)
                    {
                        Debug.LogException(e);
                        Debug.LogError("failed to destroy win:" + win);
                    }
                }
            }
            this.rootWindows.Clear();
        }
    }

    public class Counter<TKey>
    {
        Dictionary<TKey, int> data = new Dictionary<TKey, int>();
        public Dictionary<TKey, int> RawData => data;
        public int Add(TKey key, int value)
        {
            int newValue = Get(key) + value;
            data[key] = newValue;
            return newValue;
        }
        public int Get(TKey key)
        {
            int count = 0;
            data.TryGetValue(key, out count);
            return count;
        }
        public void Clear()
        {
            this.data.Clear();
        }
    }

    public class WindowManager : MonoBehaviour
    {
        [SerializeField] WindowsSortExportAsset windowsOrderConf;

        Dictionary<string, int> winType2Order = new Dictionary<string, int>();
        public int GetOrder(string windowType)
        {
            if (this.winType2Order.TryGetValue(windowType, out var order)) return order;
            UnityEngine.Debug.LogError("windowType's order not found:" + windowType);
            return 999;
        }
        public class WindowNavigationData
        {
            public BaseWindow window;
            public object data;
            public WindowNavigationData(BaseWindow window, object data = null)
            {
                this.window = window;
                this.data = data;
            }
        }

        BaseWindow _cur;
        /// <summary>
        /// 注意，当没有focus的时候，不会成为Current。
        /// </summary>
        public BaseWindow CurrentFocusWindow {
            get {
                return _cur;
            }
            set {
                if (_cur != value)
                {
                    var lastFocusWin = _cur;
                    var rawNewWin = value;
                    var newWin = value;
                    UnityEngine.Debug.Log($"Current set from `{lastFocusWin}` to `{newWin}`");

                    if (lastFocusWin != null)
                    {
                        lastFocusWin.StashFocus();  // 这里stash应该已经晚了……加载出来新界面后，可能已经设置focus到新界面了。
                        lastFocusWin._SetInteractable(false, false, false);

                        if (newWin == null)
                        {
                            lastFocusWin.IsInFocusStack = false;
                            this.FocusWindowList.Remove(lastFocusWin);
                        }
                    }

                    if (newWin != null)
                    {
                        if (newWin.IsInFocusStack)
                        {
                            this.ReorderWindowInStack(newWin);
                        } else
                        {
                            newWin.IsInFocusStack = true;
                            this.FocusWindowList.Add(newWin);
                        }
                        newWin._SetInteractable(true, false, true);  // 在动画开始之前就设置为可交互，避免状态切换时的闪烁。
                    } else
                    {
                        newWin = this.GetFirstFocusInStack();
                        if (newWin != null)
                        {
                            // 确保可以交互
                            newWin._SetInteractable(true, false, true);
                        }
                    }

                    _cur = newWin;
                    UnityEngine.Debug.Log($"Current set from `{lastFocusWin}` to `{rawNewWin}`(Effective:{newWin})");
                }
            }
        }
        List<BaseWindow> FocusWindowList = new List<BaseWindow>();// 记录哪些win有focus，以便在view关闭时找到之前的focus window。
        // 将一个已经在stack上的window，提升到stack顶部。
        void ReorderWindowInStack(BaseWindow win)
        {
            if (win == null || FocusWindowList.Count <= 1)
            {
                return;
            }

            int index = FocusWindowList.IndexOf(win);
            if (index == -1)
            {
                UnityEngine.Debug.LogError("Window is not in the list!");
                return;
            }

            // 移除并重新添加到末尾（栈顶）
            FocusWindowList.RemoveAt(index);
            FocusWindowList.Add(win);
        }

        [Tooltip("窗口不在focus的时候，变为不可交互。")]
        public bool MakeNonInteractable = true;
        [Tooltip("窗口关闭多少秒后，自动移除该窗口的实例")]
        public int destroyDelaySeconds = 30;


        /// <summary>
        /// WindowHide and Show events
        /// </summary>
        public Action<BaseWindow> onWindowShow, onWindowHide;

        // 未实例化的window
        private Dictionary<System.Type, BaseWindow> type2windowSingleInstance = new Dictionary<System.Type, BaseWindow>();
        private List<BaseWindow> windowInstances = new List<BaseWindow>();
        public Dictionary<LayerType, Layer> layerType2layer = new Dictionary<LayerType, Layer>();

        // 自动释放window prefab的asset
        Counter<GameObject> assetInstanceCounter = new Counter<GameObject>();

        protected virtual void Awake()
        {
            Initialize();
        }
        void addLayers()
        {
            // 特殊的主界面
            AddLayer(LayerType.SceneUI, null);

            // 主UI
            AddLayer(LayerType.MainUI, new Layer() { layersToClear = new LayerType[] { LayerType.FloatingPanel } });
            AddLayer(LayerType.IndependentView, new Layer() { AutoClear = false, IsMutex = false, });

            // 弹出窗口
            AddLayer(LayerType.MutexWindow, new Layer() { IsMutex = true, layersToClear = new LayerType[] { LayerType.MultiWindow, LayerType.FloatingPanel } });
            AddLayer(LayerType.MultiWindow, new Layer() { layersToClear = new LayerType[] { LayerType.MutexWindow, LayerType.FloatingPanel } });
            AddLayer(LayerType.Additive, new Layer() { layersToClear = new LayerType[] { LayerType.FloatingPanel } });

            // 特殊窗口
            AddLayer(LayerType.FloatingPanel, new Layer() { IsMutex = true });
            AddLayer(LayerType.ModelWindow, new Layer() { IsMutex = true, layersToClear = new LayerType[] { LayerType.FloatingPanel } });
            AddLayer(LayerType.Notice, new Layer() { AutoClear = false, IsMutex = false, });
        }
        public delegate UniTask<GameObject> WindowLoadFunc(string name);
        public delegate void WindowUnloadFunc(GameObject go);
        WindowLoadFunc windowAssetLoader;
        WindowUnloadFunc windowAssetUnloader;
        public void SetWindowAssetLoader(WindowLoadFunc loader, WindowUnloadFunc unloader)
        {
            this.windowAssetLoader = loader;
            this.windowAssetUnloader = unloader;
        }
        public void KillWindow(BaseWindow win)
        {
            if (win == null) return;
            if (win.IsOpened) this.Close(win);
            if (win.IsDestroyed == false)
            {
                this.DestroyWindowInstance(win);
            }
        }
        public void Close(System.Type name, bool isTry = false)
        {
            BaseWindow window;
            if (this.type2windowSingleInstance.TryGetValue(name, out window))
            {
                if (window.IsCloseAllowed())
                {
                    this.Close(window);
                    return;
                } else if (!isTry)
                {
                    UnityEngine.Debug.LogError($"failed to close window by name `{name}`, as this window is not allowed to close in current status:" + window.visibility);
                }
            } else if (!isTry)
            {
                UnityEngine.Debug.LogError($"failed to close window by name `{name}`, as no window found by this name. make sure this window exists, and is of single instance.");
            }
        }
        // 只需要关闭一个窗口的时候，调用该函数。
        public void Close(BaseWindow closingWin)
        {
            var closingWinLayer = this.layerType2layer[closingWin.LayerType];
            this.NotifyLayerChanged(closingWin.LayerType);

            // 挪走要关闭的窗口
            if (closingWin.IsMainView)
            {
                closingWinLayer.rootWindows.Remove(closingWin);
            }
            closingWin._CloseWindowByMgr();
            if (closingWin.autoDestroy)
            {
                closingWin.destroyCoroutine = StartCoroutine(DelayedDestroy(closingWin));
            }

            // 设置新的Current
            if (CurrentFocusWindow == closingWin)
            {
                this.CurrentFocusWindow = null;
            }

            // 重排序
            try
            {
                this.SortAllWindows();
            } catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
                // 先忽略一下，提升demo体验感
            }

            Debug("New Current:" + CurrentFocusWindow);
            closingWin.CloseChildren();
        }
        BaseWindow GetFirstFocusInStack()
        {
            // 找到下层第一个有focus的窗口
            while (this.FocusWindowList.Count > 0)
            {
                var win = this.FocusWindowList[this.FocusWindowList.Count - 1];
                if (win != null && win.IsOpened && win.NeedFocusManagement)
                {
                    return win;
                } else
                {
                    this.FocusWindowList.RemoveAt(this.FocusWindowList.Count - 1);
                    continue;
                }
            }
            return null;
        }
        IEnumerator DelayedDestroy(BaseWindow win)
        {
            if (win.IsDestroyed) yield break;
            if (win.SingleInstance)
            {
                yield return new WaitForSeconds(destroyDelaySeconds);
                if (win.IsDestroyed) yield break;  // 退出到主菜单的时候，被即刻清除了。
                if (win.IsOpened) yield break;
            }
            this.DestroyWindowInstance(win);
        }
        public void DestroyWindowInstance(BaseWindow win)
        {
            if (win.IsDestroyed)
            {
                return;
            }
            if (windowInstances != null) windowInstances.Remove(win);

            var asset = win.windowAsset;
            var cur = this.assetInstanceCounter.Add(asset, -1);
            if (cur == 0)
            {
                this.windowAssetUnloader(asset);
            } else if (cur < 0)
            {
                UnityEngine.Debug.LogError("code bug, counter:" + cur + " asset:" + asset);
            }

            win.DestroyThisInstance();
        }
        void AddWindowToLayer(BaseWindow win)
        {
            var layer = this.layerType2layer[win.LayerType];

            // 处理mutex
            if (layer.IsMutex)
            {
                layer.ClearWindows();
            }

            // 加入新window
            var windows = layer.rootWindows;
            if (!windows.Contains(win))
            {
                windows.Add(win);
                this.NotifyLayerChanged(win.LayerType);
            }
        }
        void SetupWindowOrder(BaseWindow win)
        {
            if (win.parent != null && !win.useFixedOrder)
            {
                win.SetSelfOrder(win.parent.EffectiveOrder + win.children.Count + 1);
                if (win.children != null && win.children.Count > 0)
                {
                    UnityEngine.Debug.LogError("unexpected children found");
                }
            } else
            {
                win.EffectiveOrder = win.baseOrder * 100;
            }
        }
        List<BaseWindow> rootWindows = new List<BaseWindow>();
        void SortAllWindows()
        {
            rootWindows.Clear();
            foreach (var p in this.layerType2layer)
            {
                foreach (var win in p.Value.rootWindows)
                {
                    if (win.parent == null)
                    {
                        rootWindows.Add(win);
                    }
                }
            }
            rootWindows.Sort((w1, w2) => {
                if (w1.openTime < w2.openTime)
                {
                    return -1;
                } else if (w1.openTime > w2.openTime)
                {
                    return 1;
                } else
                {
                    return 0;
                }
            });

            // 设置每个窗口的顺序。
            int baseOrder = 0;
            int index = 0;
            foreach (var win in rootWindows)
            {
                win.RootIndex = index;
                index++;

                baseOrder++;
                win.SetupOrder(ref baseOrder);
            }
        }
        Layer GetLayer(LayerType t)
        {
            try
            {
                return this.layerType2layer[t];
            } catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("LayerType not found in layerType2layer:" + t);
                throw e;
            }
        }
        /// <summary>
        /// Initializes the class and fills the window list with children ( executed automatically on Awake() )
        /// </summary>
        private void Initialize()
        {
            instance = this;
            preInit();
            addLayers();

            foreach (var v in this.windowsOrderConf.orders)
            {
                this.winType2Order[v.windowType] = v.order;
            }
        }
        public Action<LayerType, Layer> AddLayer;
        public static WindowManager instance;
        void preInit()
        {
            this.AddLayer = delegate (LayerType type, Layer layer) {
                if (layer == null) layer = new Layer();
                this.layerType2layer[type] = layer;
            };
        }
        void InitSingleWindow(System.Type windowKey, BaseWindow window)
        {
            if (window.LayerType == LayerType.None)
            {
                UnityEngine.Debug.LogError("LayerType is None for window:" + windowKey);
                return;
            }
            window.Initialize(this);

            // 支持覆盖sorting            
            // 这里不生效也不会报错，修改要生效的条件如下：1.activeInHierarchy=true 2.enabled=true 3.必须是sub-canvas
            // window.canvas.overrideSorting = true;

            type2windowSingleInstance[windowKey] = window;
        }
        public BaseWindow GetSingleInstance(System.Type name)
        {
            BaseWindow win;
            if (this.type2windowSingleInstance.TryGetValue(name, out win))
            {
                if (win.SingleInstance) return win;
                UnityEngine.Debug.LogError("the window named `" + name + "` is not of SingleInstance");
            } else
            {
                UnityEngine.Debug.LogError("the window named `" + name + "` is not found");
            }
            return null;
        }
        void NotifyLayerChanged(LayerType layerType)
        {
            var layer = this.GetLayer(layerType);
            if (layer.layersToClear == null) return;
            foreach (var l in layer.layersToClear)
            {
                this.GetLayer(l).ClearWindows();
            }
        }
        public bool IsUsingJoystick { get; private set; }
        public void SetJoystickModeOn(bool isOn)
        {
            this.IsUsingJoystick = isOn;
            if (isOn)
            {
                foreach (var win in this.FocusWindowList)
                {
                    if (win == this.CurrentFocusWindow)
                    {
                        win._SetInteractable(true, false, true);
                    } else
                    {
                        win._SetInteractable(false, false, false);
                    }
                }
            } else
            {
                foreach (var win in this.FocusWindowList)
                {
                    win._SetInteractable(true, false, true);
                }
            }
        }
        public async UniTask<WindowType> Open<WindowType>(object data = null, System.Action<WindowType> initFunc = null, System.Type parentName = null,
            bool isSingleton = true)
            where WindowType : BaseWindow
        {
            try
            {
                var winName = typeof(WindowType);
                var t = this.Open(winName, data: data, callback: (BaseWindow baseWindow) => {
                    initFunc?.Invoke(baseWindow as WindowType);
                }, parentWindowName: parentName, isSingleton: isSingleton);
                var view = await t;
                return view as WindowType;
            } catch (System.Exception e)
            {  // 避免吞异常
                UnityEngine.Debug.LogException(e);
                throw;
            }
        }

        public async UniTask<WindowType> Open<WindowType, ParentWindowType>(object data = null, System.Action<WindowType> initFunc = null) where WindowType : BaseWindow where ParentWindowType : BaseWindow
        {
            return await this.Open<WindowType>(data: data, initFunc: initFunc, parentName: typeof(ParentWindowType));
        }
        public async UniTask<BaseWindow> OpenAsync(System.Type windowName, object data = null, System.Action<BaseWindow> callback = null)
        {
            return await this.Open(windowName, data: data, callback: callback);
        }
        public void OpenAsyncXLua(System.Type windowName, System.Type parentWindowName, object data, XLua.LuaFunction cb)
        {
            if (cb == null)
            {
                _ = this.Open(windowName, data: data, parentWindowName: parentWindowName);
            } else
            {
                _ = this.Open(windowName, data: data, callback: (view) => {
                    cb.Action(view);
                }, parentWindowName: parentWindowName);
            }
        }
        public async UniTask<GameObject> GetWindowAsset(System.Type windowType)
        {
            GameObject windowAsset;
            if (this.windowAssetLoader == null)
            {
                UnityEngine.Debug.LogError("windowAssetLoader is empty, loading window:" + windowType);
            }
            windowAsset = await this.windowAssetLoader(windowType.Name);  // todo 这里始终不会release，存在内存泄露
            if (this == null) return null;  // 游戏已经退出了……
            if (windowAsset == null)
            {
                UnityEngine.Debug.LogError("windowLoader returns null for name:" + windowType);
                return null;
            }
            return windowAsset;
        }
        public bool IsLoading<WindowType>() where WindowType : BaseWindow
        {
            return this.loadingWindow.ContainsKey(typeof(WindowType));
        }
        public bool IsSingletonLoaded<WindowType>() where WindowType : BaseWindow
        {
            return this.GetOpenedSingleton<WindowType>() != null;
        }
        // 打开或者返回已经打开的窗口
        public async UniTask<TWindow> GetView<TWindow>(object data = null) where TWindow : BaseWindow
        {
            var view = this.GetOpenedSingleton(typeof(TWindow));
            if (view != null)
            {
                if (view.IsOpened == false)
                {
                    this.Open(view, data: data);
                } else
                {
                    if (data != null)
                    {
                        view.SetData(data, true);
                    }
                }
                return view as TWindow;
            }
            return await this.Open<TWindow>(data: data);
        }

        public TWindow GetOpenedSingleton<TWindow>() where TWindow : class
        {
            return this.GetOpenedSingleton(typeof(TWindow)) as TWindow;
        }
        // 获得window实例
        public BaseWindow GetOpenedSingleton(System.Type windowName)
        {
            BaseWindow instance;
            if (this.type2windowSingleInstance.TryGetValue(windowName, out instance) && instance != null)
            {
                if (instance.IsOpened)
                {
                    return instance;
                }
            }
            return null;
        }

        // 获得window实例
        public async UniTask<BaseWindow> GetOrCreate(System.Type winType, bool isSubView)
        {
            BaseWindow instance;
            if (this.type2windowSingleInstance.TryGetValue(winType, out instance) && instance != null)
            {
                return instance;
            }

            // 获得window asset
            //if (winType.Name == "ActionRecipeView") UnityEngine.Debug.LogError("~");
            var windowAsset = await this.GetWindowAsset(winType);
            if (windowAsset == null)
            {
                UnityEngine.Debug.LogError("winType not found:" + winType + ", name:" + winType.Name);
                return null;
            }
            var conf = windowAsset.GetComponent<BaseWindow>();

            // 实例化
            windowAsset.SetActive(false);  // to avoid OnEnable
            instance = Instantiate(windowAsset).GetComponent<BaseWindow>();
            instance.IsSubView = isSubView;
            windowAsset.SetActive(true);
            instance.transform.SetParent(this.transform, false);
            if (conf.SingleInstance)
            {
                this.InitSingleWindow(winType, instance);
            } else
            {
                instance.Initialize(this);
            }

            instance.windowAsset = windowAsset;
            assetInstanceCounter.Add(windowAsset, 1);

            windowInstances.Add(instance);

            instance.InitInstance();
            return instance;
        }
        Dictionary<System.Type, AsyncLazy<BaseWindow>> loadingWindow = new Dictionary<System.Type, AsyncLazy<BaseWindow>>();
        public static bool IsOpeningWindow = false;
        /// <summary>
        /// Shows specified window
        /// </summary>
        /// <param name="winType"></param>
        public async UniTask<BaseWindow> Open(System.Type winType, object data = null, System.Action<BaseWindow> callback = null, System.Type parentWindowName = null, bool isSingleton = true)
        {
            if (isSingleton && loadingWindow.TryGetValue(winType, out var continueTask)) return await continueTask;

            // 获得window实例
            try
            {
                IsOpeningWindow = true;
                BaseWindow parentWindow = null;
                if (parentWindowName != null)
                {
                    if (this.type2windowSingleInstance.TryGetValue(parentWindowName, out parentWindow) && parentWindow != null)
                    {
                        // ok
                    } else
                    {
                        throw new Exception($"parent window not found:`{parentWindowName}`, trying to open `{winType}`");
                    }
                }
                var task = GetOrCreate(winType, parentWindow != null);
                BaseWindow instance;
                if (isSingleton)
                {
                    var lazyTask = task.ToAsyncLazy();
                    this.loadingWindow[winType] = lazyTask;
                    instance = await lazyTask;
                } else
                {
                    instance = await task;
                }
                if (isSingleton) this.loadingWindow.Remove(winType);
                if (parentWindow != null)
                {
                    if (instance.LayerType != LayerType.IndependentView)
                    {
                        UnityEngine.Debug.LogError("invalid layer type for window:" + winType);
                        instance.LayerType = LayerType.IndependentView;
                    }
                    instance.IsSubView = true;
                    parentWindow.AddChild(instance);
                }
                Open(instance, data);
                callback?.Invoke(instance);
                return instance;
            } catch (Exception e)
            {
                // 不过不catch并自己打印异常，exception就不会显示在console里面，默默没了。Addressables的坑。
                // 应该是因为async/await机制，导致异常无法被unity捕捉到。
                UnityEngine.Debug.LogException(e);
                return null;
            } finally
            {
                IsOpeningWindow = false;
            }
        }
        /// <summary>
        /// Shows specified window ( Use Show("MyWindowName"); instead )
        /// </summary>
        /// <param name="window"></param>
        public void Open(BaseWindow window, object data = null)
        {
            if (window == null)
            {
                throw new KeyNotFoundException("WindowManager: Open(BaseWindow) failed, window is Null.");
            }

            // 在执行其他逻辑之前，先保存当前的focus window的focus object。
            if (this.CurrentFocusWindow != null)
            {
                CurrentFocusWindow.StashFocus();
            }

            // 如果正在删除，则终止删除流程
            if (window.destroyCoroutine != null)
            {
                this.StopCoroutine(window.destroyCoroutine);
                window.destroyCoroutine = null;
            }

            // 重复打开时:1.刷新数据 2.刷新CurrentFocusWindow
            if (window.IsOpened)
            {
                window.SetData(data, true);
                if (window.IsOpened == false)
                {
                    // 刷新数据的时候被关闭
                    return;
                }
                this.SetupWindowOrder(window);

                // 设置新的Current
                if (window.NeedFocusManagement)
                {
                    CurrentFocusWindow = window;
                }
                return;
            }

            // 处理需要自动关闭的layer。
            this.NotifyLayerChanged(window.LayerType);

            // 加入新窗口
            if (window.IsMainView)
            {
                this.AddWindowToLayer(window);
            }
            if (!window.IsOpened)
            {
                window.OpenWindow(data);
                this.SetupWindowOrder(window);  // 等active状态之后，再设置canvas
            }

            // 设置新的Current
            if (window.NeedFocusManagement)
            {
                CurrentFocusWindow = window;
            }
            return;
        }

        /// <summary>
        /// Hides all screns of specific type ( or removes them when they're a copy )
        /// </summary>
        public void CloseAll(bool deleteInstance = false)
        {
            this.ClearFocusWindows();
            foreach (var p in this.layerType2layer)
            {
                try
                {
                    p.Value.ClearWindows(this, deleteInstance: deleteInstance);
                } catch (System.Exception e)
                { // 避免卡死
                    UnityEngine.Debug.LogException(e);
                    UnityEngine.Debug.LogError("failed to clear layer:" + p.Key);
                }
            }
            foreach (var p in this.type2windowSingleInstance)
            {
                try
                {
                    this.KillWindow(p.Value);
                } catch (System.Exception e)
                { // 避免卡死
                    UnityEngine.Debug.LogException(e);
                    UnityEngine.Debug.LogError("failed to clear layer:" + p.Key);
                }
            }
            this.type2windowSingleInstance.Clear();

            var list = this.windowInstances;
            this.windowInstances = null;
            foreach (var inst in list)
            {// 按理说执行这一个for就能删掉所有win了
                this.KillWindow(inst);
            }

            list.Clear();
            this.windowInstances = list;
        }
        void ClearFocusWindows()
        {
            foreach (var win in this.FocusWindowList) win.IsInFocusStack = false;
            this.FocusWindowList.Clear();
            this.CurrentFocusWindow = null;
        }
        public void CloseAllNonEssential()
        {
            this.ClearFocusWindows();
            foreach (var p in this.layerType2layer)
            {
                if (p.Key > LayerType.MainUI)
                {
                    p.Value.ClearWindows();
                }
            }
        }

        [System.Diagnostics.Conditional("DEBUG_WindowManager")]
        private void Debug(string str)
        {
            UnityEngine.Debug.Log("[WindowManager] WindowManager: <b>" + str + "</b>", this);
        }
        /*
        public void OnCancel(CallbackContext ctx) {
            if (ctx.performed) {
                if (this.Current != null && this.Current.CancelToClose) {
                    this.Close(this.Current);
                }
            }
        }*/
    }

}
