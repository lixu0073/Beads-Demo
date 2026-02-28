using UnityEngine;

namespace Aor.UI
{
    public class GuiSorting :MonoBehaviour
    {
        [SerializeField] Renderer targetRenderer;
        [SerializeField] Canvas targetCanvas;
        [SerializeField] GuiSorting[] children;

        int selfOrder;
        private void Awake() {
            if (targetRenderer != null) selfOrder = targetRenderer.sortingOrder;
            else if (targetCanvas != null) selfOrder = targetCanvas.sortingOrder;
        }
        public void SetOrder(int order) {
            if (targetRenderer != null) targetRenderer.sortingOrder = order + selfOrder;
            else if (targetCanvas != null) targetCanvas.sortingOrder = order + selfOrder;
            if (children != null) {
                foreach (var child in children) child.SetOrder(order);
            }
        }
    }
}