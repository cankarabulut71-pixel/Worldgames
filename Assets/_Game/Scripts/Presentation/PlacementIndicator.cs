using UnityEngine;
using UnityEngine.Rendering;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// A soft ring lying on the cell under the pointer, coloured by what a tap would do there
    /// (plant, look, remove, or not possible). Glides between cells and fades in and out.
    /// </summary>
    public sealed class PlacementIndicator : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        private Material material;
        private MeshRenderer meshRenderer;
        private Vector3 target;
        private Vector3 targetNormal = Vector3.up;
        private float size;
        private Color color = Color.white;
        private float alpha;
        private float targetAlpha;
        private bool placed;
        private bool pressed;
        private Vector3 position;
        private Vector3 normal = Vector3.up;
        private float clock;

        public void Init(Material template, float size)
        {
            this.size = size;
            material = new Material(template) { name = "Placement Marker" };
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_Shape", 1f);
            material.SetFloat("_RingWidth", 0.13f);
            material.renderQueue = (int)RenderQueue.Transparent + 10;

            gameObject.AddComponent<MeshFilter>().sharedMesh = Quad();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            Apply();
        }

        /// <summary>Shows the ring at a point on the ground, facing out along <paramref name="up"/>.</summary>
        public void Show(Vector3 point, Vector3 up, Color tint, bool isPressed)
        {
            if (!placed || alpha < 0.02f)
            {
                position = point;
                normal = up;
            }
            placed = true;
            targetAlpha = tint.a;
            color = new Color(tint.r, tint.g, tint.b, 1f);
            pressed = isPressed;
            target = point;
            targetNormal = up;
        }

        public void Hide() => targetAlpha = 0f;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            clock += dt;
            float k = 1f - Mathf.Exp(-dt * 20f);
            position = Vector3.Lerp(position, target, k);
            normal = Vector3.Slerp(normal, targetNormal, k).normalized;
            alpha = Mathf.MoveTowards(alpha, targetAlpha, dt * 6f);
            Apply();
        }

        private void Apply()
        {
            float pulse = 1f + Mathf.Sin(clock * 4f) * 0.05f;
            float scale = size * pulse * (pressed ? 0.85f : 1f);
            transform.SetPositionAndRotation(position + normal * 0.06f, Quaternion.FromToRotation(Vector3.up, normal));
            transform.localScale = new Vector3(scale, 1f, scale);
            var tint = color;
            tint.a = alpha;
            material.SetColor(TintId, tint);
            meshRenderer.enabled = alpha > 0.01f;
        }

        private static Mesh Quad()
        {
            var mesh = new Mesh { name = "Marker" };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f),
            });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            mesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
