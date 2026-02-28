using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Aor.UI
{
    public enum WindowVisibility
    {
        Invisible, TransitionIn, Visible, TransitionOut
    }
    public static class WindowVisibilityExt
    {
        public static bool IsTransition(this WindowVisibility v)
        {
            return v == WindowVisibility.TransitionIn || v == WindowVisibility.TransitionOut;
        }
        public static bool IsVisible(this WindowVisibility v)
        {
            return v == WindowVisibility.Visible;
        }
        public static bool IsInvisible(this WindowVisibility v)
        {
            return v == WindowVisibility.Invisible;
        }
    }

    [RequireComponent(typeof(CanvasGroup))]
    public class BaseWindow : MonoBehaviour
    {

#if DEBUG
#pragma warning disable CS0414 // 字段“BaseWindow.debug_isOpening”已被赋值，但从未使用过它的值
        [SerializeField] bool debug_isOpening;
#pragma warning restore CS0414 // 字段“BaseWindow.debug_isOpening”已被赋值，但从未使用过它的值
#endif
        [Header("Important Settings")]
        public LayerType LayerType;  // 层类型
        [Tooltip("该窗口是否需要Focus。设置为false会让整个窗口不可交互。")]
        public bool IsInteractable = true;
        [HideInInspector]
        public bool IsSubView;
        public bool IsMainView => !IsSubView;
        // true表示可以成为Current窗口，可以通过手柄进行操纵
        public bool NeedFocusManagement {
            get {
                return this.IsInteractable && this.FocusReady;
            }
        }
        [NonSerialized]
        public bool IsInFocusStack;
        int order = -1;
        public int baseOrder {
            get {
                if (order < 0)
                {
                    this.order = this.mgr.GetOrder(this.GetType().Name);
                }
                return this.order;
            }
        }
        [SerializeField] public bool useFixedOrder;

        public Layer layer {
            get {
                return this.mgr.layerType2layer[LayerType];
            }
        }

        [Header("Parameters")]
        [Tooltip("只能一个实例。如果为false，则每次都会创建新的实例。")]
        public bool SingleInstance = true;
        [Tooltip(" 可以不展示其他界面，节省性能   ")]
        public bool BlockUnderneathUI = false;
        [Tooltip("不要自动销毁")]
        public bool autoDestroy = true;
        [Tooltip("自动绑定关闭事件。")]
        public Button closeButton;
        [Tooltip("关闭窗口并重新打开之后，是否还原上次的Focus。")]
        public bool ReopenWithLastFocus;
        [Tooltip("是否准备好了进行手柄操作。")]
        public bool FocusReady = false;

        [Tooltip("是否可以通过UICancel来快速关闭")]
        public bool CancelToClose = true;

        public GameObject firstSelected;
        public GameObject[] firstSelectedList;
        private GameObject TargetFocus {
            get {
                var recoverFocus = this.GetRecoverFocus();
                if (recoverFocus != null && recoverFocus.activeInHierarchy)
                {
                    return recoverFocus;
                }
                return ActiveConfigFocus;
            }
        }
        // true表示在界面打开的时候，如果focus还未被其他地方设置过，则需要自动设置focus。
        public bool AutoFocus {
            get {
                if (this.firstSelected != null) return true;
                if (this.firstSelectedList != null && this.firstSelectedList.Length > 0) return true;
                return false;
            }
        }

        GameObject ActiveConfigFocus {
            get {
                if (this.firstSelected != null && this.firstSelected.activeInHierarchy) return this.firstSelected;
                if (this.firstSelectedList != null)
                {
                    foreach (var fs in this.firstSelectedList)
                    {
                        if (fs != null && fs.activeInHierarchy) return fs;
                    }
                }
                return null;
            }
        }
        public bool HasFocusGameObject {
            get {
                var go = EventSystem.current.currentSelectedGameObject;
                return go != null && go.activeInHierarchy;
            }
        }

        public System.Action onCloseEvent;  // 不用event，event还得 -= 注销，太恶心了

        [HideInInspector]
        protected object data;  // 打开窗口的参数
        [HideInInspector]
        internal Coroutine destroyCoroutine;
        [HideInInspector]
        internal GameObject windowAsset;

        #region 销毁实例
        public bool IsDestroyed => this == null || this.gameObject == null || instanceDestroyed;
        bool instanceDestroyed = false;
        public void DestroyThisInstance()
        {
            GameObject.Destroy(this.gameObject);  // 同一帧中destroy不会立刻销毁
            this.windowAsset = null;
            instanceDestroyed = true;
        }
        #endregion

        // 是否是当前的焦点窗口（可以接收到输入）
        public bool IsFocused {
            get {
                if (canvasGroup == null)
                {
                    Debug.LogError("canvasGroup cannot be null");
                    return false;
                }
                return canvasGroup.interactable && canvasGroup.blocksRaycasts && this.NeedFocusManagement;
            }
        }

        /// <summary>
        /// 不考虑动画，当调用close之后，isOpened立刻为false。如果需要考虑动画，使用isVisible来判断。
        /// </summary>
        public bool IsOpened {
            get; private set;
        }
        public bool IsOpening {
            get; private set;
        }
        /// Unique ID for all BaseWindows
        [HideInInspector]
        public readonly int ID = UniqueID<BaseWindow>.NextUID;

        /// <summary>
        /// Is transitioning?
        /// </summary>
        public bool IsTransitioning {
            get {
                return gameObject.activeSelf && this.visibility.IsTransition();
            }
        }

        // Internal functions, DO NOT TOUCH !
        public WindowVisibility visibility { get; protected set; }
        protected WindowManager mgr;
        protected GameObject recoverFocus;  // 再次需要focus的时候，可以使用此值。
        protected GameObject ManualFocus;  // 手动设置的focus
        protected GameObject ActiveManualFocus {
            get {
                if (this.ManualFocus == null) return null;
                if (this.ManualFocus.gameObject.activeInHierarchy)
                {
                    return this.ManualFocus;
                }
                return null;
            }
        }

        protected CanvasGroup canvasGroup;
        public Canvas canvas { get; private set; }
        protected object transitionData;

        // 窗口之间的父子关系
        [HideInInspector]
        public BaseWindow parent;
        [HideInInspector]
        public List<BaseWindow> children;
        [HideInInspector]
        public float openTime;  // 用来排序。后打开的页面，需要放到先打开的页面上面。

        bool blockRaycast;

        /// <summary>
        /// Initialized by WindowManager automatically, do not do it yourself unless necessary 
        /// </summary>
        /// <param name="mgr">Window Manager</param>
        internal void Initialize(WindowManager mgr)
        {
            if (!this.gameObject.TryGetComponent<Canvas>(out Canvas existingCanvas))
            {
                this.canvas = this.gameObject.AddComponent<Canvas>();
            } else
            {
                this.canvas = existingCanvas;
            }
            if (!this.gameObject.TryGetComponent<GraphicRaycaster>(out GraphicRaycaster caster))
            {
                this.gameObject.AddComponent<GraphicRaycaster>();
            }
            this.mgr = mgr;
            canvasGroup = gameObject.GetComponentInChildren<CanvasGroup>();
            if (canvasGroup) this.blockRaycast = this.canvasGroup.blocksRaycasts;

            _InnerSetInteractable(false, false);
            gameObject.SetActive(false);

            if (this.closeButton)
            {
                this.closeButton.onClick.AddListener(this.Close);
            }
        }
        internal bool IsCloseAllowed()
        {
            return this.visibility == WindowVisibility.Visible || this.visibility == WindowVisibility.TransitionIn;
        }
        internal void AddChild(BaseWindow child)
        {
            if (this.children == null) this.children = new List<BaseWindow>();
            child.parent = this;
            this.children.Add(child);
            Debug.Log($"add window:{child} to parent:{this}");
        }
        internal void RemoveChild(BaseWindow child)
        {
            Debug.Assert(child.parent != null);
            child.parent = null;
            Debug.Log($"remove window:{child} from parent:{this}");
            this.children.Remove(child);
        }
        internal void CloseChildren(bool deleteInstance = false)
        {
            if (this.children == null) return;
            while (this.children.Count > 0)
            {  // 这里不能直接遍历
                var win = children[children.Count - 1];
                if (win.IsOpened)
                {
                    win.Close();  // bug-fix: 子窗口用 ViewOpener 的时候，被关闭时还是用 Close() 会导致ViewOpener逻辑问题。
#if DEBUG
                    if (win.IsOpened)
                    {
                        Debug.LogError("failed to close sub-view");
                        break;
                    }
#endif
                    if (deleteInstance) mgr.DestroyWindowInstance(win);
                } else
                {
                    Debug.Log($"remove window:{win} from parent:{this}");
                    children.RemoveAt(children.Count - 1);
                }
            }
        }
        internal int RootIndex;  // 在所有1级窗口（没有父节点的窗口）列表中的index
        internal void SetupOrder(ref int baseOrder)
        {
            this.SetSelfOrder(baseOrder + this.baseOrder * 100);
            if (this.children != null)
            {
                this.children.Sort((w1, w2) => {
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
                foreach (var child in this.children)
                {
                    baseOrder++;
                    child.SetupOrder(ref baseOrder);
                }
            }
        }
        int effectiveOrder;
        public int EffectiveOrder {
            get {
                return this.canvas.sortingOrder;
            }
            set {
                if (this == null || this.gameObject == null)
                {
                    Debug.LogError("code bug?");
                    return;
                }
                if (this.enabled && this.gameObject.activeInHierarchy && this.canvas.isRootCanvas == false)
                {
                    this.canvas.overrideSorting = true;
                    this.canvas.sortingLayerName = "UI";
                    // order最大值3w，最多10个layer，每个layer最多20个window，每个window有 3w/10/20=150 的空间可以自由发挥。可以用来放3D模型和ParticleSystem。
                    this.canvas.sortingOrder = value;
                    if (this.TryGetComponent<GuiSorting>(out GuiSorting sorting))
                    {
                        sorting.SetOrder(value);
                    }
                    this.OnSortingOrderChanged(this.canvas.sortingOrder);
                } else if (EditorUtil.IsEditor)
                {
                    Debug.LogError("code bug, UI窗口不应该被隐藏。" + GameObjectUtil.GetPath(this.transform));
                }
            }
        }
        public void SetSelfOrder(int order)
        {
            this.EffectiveOrder = order;
        }
        public void SetCloseButton(Button btn)
        {
            if (this.closeButton != null)
            {
                this.closeButton.onClick.RemoveAllListeners();
                this.closeButton = null;
            }
            if (btn != null)
            {
                this.closeButton = btn;
                this.closeButton.onClick.AddListener(this.CloseWindowOnly);
            }
        }
        bool initDone;
        internal void InitInstance()
        {
            if (initDone) return;
            this.initDone = true;
            if (this.canvasGroup != null && this.NeedFocusManagement)
            {
                this.canvasGroup.interactable = this.IsInteractable;
            }
            this.OnInit();
        }

        /// <summary>
        /// called only once for a single Window instance.
        /// </summary>
        protected virtual void OnInit()
        {

        }

        public virtual void SetTransitionData(object data)
        {
            this.transitionData = data;
        }

        /// <summary>
        /// called after show-animation is finished
        /// </summary>
        protected virtual void OnShowAnimationDone()
        {
            // Do nothing...
        }

        /// <summary>
        /// called after hide-animation is finished
        /// </summary>
        protected virtual void OnHideAnimationDone()
        {
            // Do nothing...
        }

        /// <summary>
        /// called before open-animation starts
        /// </summary>
        protected virtual void OnOpen()
        {

        }

        // 给业务层的框架逻辑使用
        protected virtual void _OnOpen()
        {

        }

        protected virtual void OnFocus()
        {

        }

        protected virtual void OnFocusLost()
        {

        }

        /// <summary>
        /// called before close-animation starts
        /// </summary>
        protected virtual void OnClose()
        {

        }

        // 给非业务逻辑使用
        protected virtual void _OnClose()
        {

        }

        // 给非业务逻辑使用
        protected virtual void OnClosed()
        {

        }

        /// <summary>
        /// Animation In starts ( basically when window appears )
        /// </summary>
        protected virtual void OnAnimationIn()
        {
            NotifyShowAnimationDone(); // Execute this at end of this animation
        }

        /// <summary>
        /// Animation Out starts ( basically when window disappears )
        /// </summary>
        protected virtual void OnAnimationOut()
        {
            OnAnimationOutEnd(); // Execute this at end of this animation
        }

        //--------------------------------------

        /// <summary>
        /// This should be executed at the end of the focus ( when window starts ) animation ( OnAnimationIn() )
        /// </summary>
        public void NotifyShowAnimationDone()
        {
            this.visibility = WindowVisibility.Visible;
            this.OnShowAnimationDone();
            Log("OnAnimation  In  End : " + name);
        }

        /// <summary>
        /// This should be executed at the end of the blur ( when window loses focus ) animation ( OnAnimationOut() )
        /// </summary>
        public void OnAnimationOutEnd()
        {
            this.OnClosed();
            gameObject.SetActive(false);
            this.visibility = WindowVisibility.Invisible;
            this.OnHideAnimationDone();
            Log("OnAnimation  Out  End : " + name);
        }
        public virtual void OnSortingOrderChanged(int newOrder)
        {
            // do nothing;
        }
        // called by user
        public void CloseWindowOnly()
        {
            if (this.IsOpening)
            {
                // todo 暂不处理……
                return;
            }
            if (!this.IsOpened)
            {
                // Debug.Log("failed to close, as already closed. window name:" + this.name);
                return;
            }
            Log("Closing:" + this.name);
            mgr.Close(this);
        }
        // 允许子类覆盖CloseButton的行为。
        public virtual void Close()
        {
            this.CloseWindowOnly();
        }
        /// <summary>
        /// 单个窗口的完整功能关闭函数，直接调用也能正确关闭。
        /// 但是不能保证多窗口逻辑的正确性。
        /// 该正确性由 WindowManager.Close() 来完成。
        /// </summary>
        /// <param name="hide">Should hide or disable?</param>
        /// <param name="destroy">Should destroy or desactivate?</param>
        internal void _CloseWindowByMgr()
        {
            if (this.IsOpened == false)
            {
                Debug.LogError($"failed to close this window:{this}, IsOpened is false");
                return;
            }
            if (this.visibility == WindowVisibility.Invisible)
            {
                Debug.LogError($"failed to close this window. invalid visibility:{this.visibility}");
                return;
            }

            if (this.visibility == WindowVisibility.TransitionOut)
            {
                this.OnAnimationOutEnd();
                return;
            }
            if (this.visibility == WindowVisibility.TransitionIn)
            {
                NotifyShowAnimationDone();
            }
            // before callback
            this.IsOpened = false;
#if DEBUG
            Debug.Log("close view:" + this.GetType());
#endif

            // 处理父子关系
            // 自己的parent。children等到已经关闭之后，再处理，免得嵌套关闭窗口造成问题。
            if (this.parent != null)
            {
                this.parent.RemoveChild(this);
            }
            this.parent = null;

            try
            {
#if DEBUG
                this.debug_isOpening = false;
#endif
                if (this.ReopenWithLastFocus == false)
                {
                    this.ManualFocus = null;
                    this.recoverFocus = null;
                    if (EventSystem.current)
                    {
                        var f = EventSystem.current.currentSelectedGameObject;
                        if (f && f.transform.IsChildOf(this.transform))
                        {
                            Log("[focus] clear focus object when closing window");
                            EventSystem.current.SetSelectedGameObject(null);
                        }
                    }
                }
                this.canvasGroup.blocksRaycasts = false;  // 让玩家立刻能点击到下面的其他GUI
                this.OnClose();
                this._OnClose();
                this.onCloseEvent?.Invoke();
            } catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }

            // after callback
            this.data = null;

#if DEBUG
            if (!this.visibility.IsVisible())
            {
                Debug.LogError("visibility:" + this.visibility);
            }
#endif
            UnityEngine.Debug.Assert(this.visibility.IsVisible());
            Log("BLUR: " + name + " (visible:" + this.visibility + ")");

            if (this.IsFocused)
            {
                var currentFocus = EventSystem.current.currentSelectedGameObject;
                if (currentFocus != null && !GameObjectUtil.IsParent(currentFocus.transform, this.transform))
                {
                    // 原因说明：如果当前页面没有focus，则EventSystem.current.currentSelectedGameObject 获取到的是上一个focus，可能这个focus在别的页面。而且，即使focus所在页面已经不再interactable了，EventSystem.current.currentSelectedGameObject取出来的值仍然是这个focus。
                    // 解决：每个页面，要么代码里面设置当前界面的focus，要么在inspector的BaseWindow里面设置 FirstSelected 字段以进行自动focus设置。
                    // Debug.LogError("current:" + this.name + ", record focus on other window:" + Aor.GameObjectUtil.GetPath(currentFocus.transform));
                } else
                {
                    this.recoverFocus = currentFocus;
                    Log("record focus:" + (this.recoverFocus == null ? "NULL" : this.recoverFocus.name));
                }
            }
            _SetInteractable(false, false, true);

            this.visibility = WindowVisibility.TransitionOut;
            OnAnimationOut();
        }
        /// <summary>
        /// DO NOT USE !
        /// Internal function executed when window gains focus
        /// </summary>
        /// <param name="show">Should show or enable?</param>
        internal void OpenWindow(object data)
        {
            this.IsOpening = true;
            if (this.visibility == WindowVisibility.TransitionOut)
            {
                OnAnimationOutEnd();
            }

            Log("FOCUS : " + name);
            this.openTime = Time.realtimeSinceStartup;
            gameObject.SetActive(true);
            try
            {
                this.SetData(data, false);  // active之后再setData，要不然Awake中的初始化没有调用会导致报错。
            } catch (System.Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("failed to set data, win:" + this);
            }

            this.IsOpened = true;
            this.visibility = WindowVisibility.TransitionIn;
            if (this.canvasGroup) this.canvasGroup.blocksRaycasts = this.blockRaycast;
            _SetInteractable(true, true, false);  // 在动画开始之前就设置为可交互，避免状态切换时的闪烁。
            this.OnOpen();
            this._OnOpen();
            if (this.ManualFocus == null)
            {  // RefreshView或者OnOpen里面，可能已经手动设置了focus。此时不要进行覆盖。
                this.AutoSetFocus(true, true);  // OnOpen之后，可能focus才进入active状态。
            }
#if DEBUG
            this.debug_isOpening = true;
#endif
            OnAnimationIn();
            this.IsOpening = false;
        }
        void AutoSelectElement(bool opening)
        {
            var useOldFocus = !opening || this.ReopenWithLastFocus;
            GameObject focus;
            if (useOldFocus)
            {
                if (this.TargetFocus == null || this.TargetFocus.activeInHierarchy)
                {
                    focus = this.TargetFocus;
                } else
                {
                    focus = this.ActiveConfigFocus;
                }
            } else
            {
                focus = this.ActiveConfigFocus;
            }
            if (focus)
            {
                Log($"[focus] auto set focus to: {focus}");
                this.Select(focus, allowOverride: false);
            } else
            {
                Log($"[focus] auto focus is null");
            }
        }
        public void SetDataManually(object data, bool isRefreshing)
        {
            this.SetData(data, isRefreshing);
        }
        internal virtual void SetData(object data, bool isRefreshing)
        {
            Log("Refreshing data");
            this.data = data;
            this.openTime = Time.realtimeSinceStartup;
            if (isRefreshing)
            {
                this.OnRefreshData();
            } else
            {
                this.OnInitData();
            }
            this.RefreshView();
        }
        protected virtual void OnInitData() { }
        protected virtual void OnRefreshData() { }
        protected virtual void RefreshView() { }
        GameObject GetRecoverFocus()
        {
            if (this.ManualFocus != null && this.ManualFocus.activeInHierarchy)
            {
                return this.ManualFocus;
            } else if (this.recoverFocus != null && this.recoverFocus.activeInHierarchy)
            {
                return this.recoverFocus;
            }
            return null;
        }
        public void StashFocus()
        {
            var focus = EventSystem.current.currentSelectedGameObject;
            if (focus != null && focus.activeInHierarchy && focus.transform.IsChildOf(this.transform))
            {
                this.ManualFocus = focus;
                this.recoverFocus = focus;
            }
        }
        public static System.Func<bool> FocusManagementSwitch;
        public bool SetFocusIfNecessary(GameObject focusGo)
        {
            if (this.HasFocusGameObject == false && focusGo.activeInHierarchy)
            {
                this.Select(focusGo);
                return true;
            }
            return false;
        }
        public bool SetManualFocus(GameObject focusGo)
        {
            if (this.ManualFocus == null && focusGo.activeInHierarchy)
            {
                this.Select(focusGo);
                return true;
            }
            return false;
        }
        public void Select(GameObject go, bool allowDisabled = false, bool allowOverride = false)
        {
            if (FocusManagementSwitch != null && FocusManagementSwitch() == false)
            {
                return;  // 有键鼠的时候，不需要focus管理。
            }
            if (!this.NeedFocusManagement)
            {
                Debug.LogError($"[{this.name}] 不允许修改focus，focus_ready:" + this.FocusReady);
                return;
            }
            // 自动恢复之前的focus
            if (allowOverride)
            {
                var recoverFocus = this.GetRecoverFocus();
                if (recoverFocus != null) go = recoverFocus;
            }
            if (go != null)
            {
                if (!go.activeInHierarchy)
                {
                    if (allowDisabled)
                    {
                        Log("[focus] failed to set focus, as it's not active in hierarchy now, focus.name:" + go.name + ", go-path:" + GameObjectUtil.GetPath(go.transform) + ", window.name:" + this.name);
                    } else
                    {
                        Debug.LogError("[focus] failed to set focus, as it's not active in hierarchy now, focus.name:" + go.name + ", go-path:" + GameObjectUtil.GetPath(go.transform) + ", window.name:" + this.name);
                    }
                    this.recoverFocus = EventSystem.current.currentSelectedGameObject;
                    return;
                }
                Log("[focus] select " + go.name);
                this.recoverFocus = go;
                this.ManualFocus = go;
                EventSystem.current.SetSelectedGameObject(go);
                Log("[focus] cur " + EventSystem.current.currentSelectedGameObject);
            } else
            {
                var cur = EventSystem.current.currentSelectedGameObject;
                if (cur != null && cur.transform.IsChildOf(this.transform))
                {
                    Log("[focus] stash focus " + cur);
                    this.recoverFocus = cur;
                    EventSystem.current.SetSelectedGameObject(null);
                }
                this.ManualFocus = null;
            }
        }
        /// <summary>
        /// Changes canvas group state of interactable and blockRaycasts instantly
        /// </summary>
        /// <param name="enabled">Should enable or disable after transition?</param>
        private void _InnerSetInteractable(bool enabled, bool opening)
        {
            if (!this.NeedFocusManagement)
            {
                Log($"set interactable to {enabled}, ignored");
                return;
            }
            if (!this.mgr.MakeNonInteractable)
            {
                Log($"set interactable to {enabled}, ignored by manager");
                return;
            }
            if (this.IsFocused == enabled)
            {
                Log($"set interactable to {enabled}, ignored by same value");
                return;
            }
            Log($"set interactable to {enabled}");
            canvasGroup.interactable = enabled;
            if (enabled)
            {
                this.OnFocus();
            } else
            {
                this.OnFocusLost();
            }
        }
        protected void AutoSetFocus(bool enabled, bool opening)
        {
            if (this.NeedFocusManagement && this.AutoFocus)
            {
                if (enabled)
                {
                    Log($"focus management enabled");
                    this.AutoSelectElement(opening);
                } else
                {
                    this.Select(null);  // 保存last focus
                }
            }
        }
        internal void _SetInteractable(bool enabled, bool opening, bool setupFocus)
        {
            if (this.mgr.IsUsingJoystick == false)
            {
                Log($"set interactable to {enabled}, ignored for non-joystick");
                return;
            }
            if (!this.NeedFocusManagement)
            {
                Log($"set interactable to {enabled}, ignored");
                return;
            }
            if (setupFocus)
            {
                this.AutoSetFocus(enabled, opening);
            }
            if (!this.mgr.MakeNonInteractable)
            {
                Log($"set interactable to {enabled}, ignored by manager");
                return;
            }
            if (this.IsFocused == enabled)
            {
                Log($"set interactable to {enabled}, ignored by same value");
                return;
            }

            this._InnerSetInteractable(enabled, opening);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        protected void Log(string str)
        {
            UnityEngine.Debug.Log($"[WindowManager][{this.name}: <b>{str}</b>", this);
        }
        /*
        public virtual void Update() {
            if (!this.initDone) return;
            var currentFocus = EventSystem.current.currentSelectedGameObject;
            if (this.IsFocused) {
                if (currentFocus != lastFocus) {
                    if (currentFocus == null) {
                        if (lastFocus.activeInHierarchy) {
                            EventSystem.current.SetSelectedGameObject(lastFocus);
                        } else {
                            lastFocus = null;  // focus lost.
                        }
                    } else {
                        lastFocus = currentFocus;
                    }
                }
            }
        }*/
    }

    public class BaseWindow<TArgs> : BaseWindow
    {
        protected TArgs args;

        internal override void SetData(object data, bool isRefreshing)
        {
            if (data != null)
            {
                args = (TArgs)data;
                base.SetData(data, isRefreshing);
            } else
            {
                base.SetData(null, isRefreshing);
            }
        }
    }
}

