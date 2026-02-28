using UnityEngine;
using UnityEngine.Events;

namespace Aor.UI
{
    // 鼠标在RectTransform外部点击的时候，响应事件
    public class ClickOutsideEvent :MonoBehaviour
    {
        [Header("点击区域配置")]
        [SerializeField] private RectTransform targetRect;
        [SerializeField] private RectTransform[] extraRects;

        [Header("响应事件")]
        [SerializeField] private UnityEvent onClickOutside;

        [Header("调试")]
        [SerializeField] private bool debug;

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (this.targetRect == null) this.targetRect = transform as RectTransform;

                if (IsClickOutside())
                {
                    if (this.debug) Debug.Log($"[ClickOutside] 点击了 {this.targetRect.name} 外部区域", gameObject);
                    this.onClickOutside?.Invoke();
                }
            }
        }

        private bool IsClickOutside()
        {
            Vector2 mousePos = Input.mousePosition;

            if (IsPointInRect(this.targetRect, mousePos)) return false;

            if (this.extraRects != null)
            {
                foreach (var rect in this.extraRects)
                {
                    if (rect != null && IsPointInRect(rect, mousePos)) return false;
                }
            }

            return true;
        }

        private bool IsPointInRect(RectTransform rect, Vector2 screenPoint)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, GetCameraForRect(rect));
        }

        private Camera GetCameraForRect(RectTransform rect)
        {
            // 获取 Canvas 的 RenderMode 以决定使用哪个相机
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }
            return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }
    }
}