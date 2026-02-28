using UnityEngine;
using UnityEngine.UI;

namespace Aor.UI
{
    // Empty 4 Raycast 不参与渲染Batching，代替Image
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("Aor/UI/Block")]
    public sealed class Block :Graphic, ICanvasRaycastFilter
    {
        public override Texture mainTexture => null;
        public override Material materialForRendering => null;

        public bool IsRaycastLocationValid(
            Vector2 screenPoint, Camera eventCamera) {
            return true;
        }

        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();
        }
    }
}