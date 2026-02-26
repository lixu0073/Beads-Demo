using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using System.Threading;

namespace Aor.GuiSolution
{
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class SafeAreaUGUI : MonoBehaviour
    {
        private RectTransform rectTransform;
        private CancellationTokenSource cts;

        private Rect lastSafeArea;
        private Vector2Int lastRes;

        public bool adjustTop = true;
        public bool adjustBottom = true;
        public bool adjustLeft = true;
        public bool adjustRight = true;

        [Range(0, 0.5f)] public float topPadding = 0f;
        [Range(0, 0.5f)] public float bottomPadding = 0f;
        [Range(0, 0.5f)] public float leftPadding = 0f;
        [Range(0, 0.5f)] public float rightPadding = 0f;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        void OnEnable()
        {
            // 初始立即刷新
            ApplySafeArea();

            if (Application.isPlaying)
            {
                cts = new CancellationTokenSource();
                ObserveChangesAsync(cts.Token).Forget();
            }
        }

        void OnDisable()
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }
        }

        private void OnValidate()
        {
            if (!rectTransform) rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

#if UNITY_EDITOR
        void Update()
        {
            if (!Application.isPlaying) ApplySafeArea();
        }
#endif

        private async UniTaskVoid ObserveChangesAsync(CancellationToken token)
        {
            await ScreenObserver.ObserveScreenChanges(token)
                .ForEachAsync((snapshot) => ApplySafeArea(snapshot), token);
        }

        public void ApplySafeArea(ScreenObserver.ScreenSnapshot? snapshot = null)
        {
            if (!rectTransform) return;

            Rect sa = snapshot?.SafeArea ?? Screen.safeArea;
            Vector2Int res = snapshot?.Resolution ?? new Vector2Int(Screen.width, Screen.height);

            if (sa == lastSafeArea && res == lastRes) return;
            lastSafeArea = sa;
            lastRes = res;

            if (res.x <= 0 || res.y <= 0) return;

            Vector2 anchorMin = sa.position;
            Vector2 anchorMax = sa.position + sa.size;

            anchorMin.x /= res.x;
            anchorMin.y /= res.y;
            anchorMax.x /= res.x;
            anchorMax.y /= res.y;

            // 垂直适配
            if (adjustBottom)
                anchorMin.y = Mathf.Clamp01(anchorMin.y + bottomPadding);
            else
                anchorMin.y = 0;

            if (adjustTop)
                anchorMax.y = Mathf.Clamp01(anchorMax.y - topPadding);
            else
                anchorMax.y = 1;

            // 水平适配
            if (adjustLeft)
                anchorMin.x = Mathf.Clamp01(anchorMin.x + leftPadding);
            else
                anchorMin.x = 0;

            if (adjustRight)
                anchorMax.x = Mathf.Clamp01(anchorMax.x - rightPadding);
            else
                anchorMax.x = 1;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}