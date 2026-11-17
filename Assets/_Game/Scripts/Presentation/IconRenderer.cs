using UnityEngine;
using UnityEngine.Rendering;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// Renders a model once into a small transparent texture for the codex. The same texture tinted
    /// a flat colour doubles as the silhouette of an undiscovered species.
    /// </summary>
    public static class IconRenderer
    {
        // Far from the planet and beyond the game camera's far plane, so nothing else is in shot.
        private static readonly Vector3 Studio = new Vector3(0f, 2000f, 0f);

        /// <summary>A white copy of an icon that keeps only its outline (alpha), to be tinted as a silhouette.</summary>
        public static Texture2D Silhouette(RenderTexture icon)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = icon;
            var texture = new Texture2D(icon.width, icon.height, TextureFormat.RGBA32, false) { name = icon.name + " Silhouette" };
            texture.ReadPixels(new Rect(0, 0, icon.width, icon.height), 0, 0);
            RenderTexture.active = previous;

            var pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, pixels[i].a);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <param name="model">A freshly built model; it is destroyed after rendering.</param>
        /// <param name="viewRotation">Usually the game camera's rotation, so the camera-mounted key light falls the same way.</param>
        public static RenderTexture Render(GameObject model, Quaternion viewRotation, int size = 256)
        {
            var texture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { name = model.name + " Icon" };
            texture.Create();

            // Facing the camera, turned a little, and tipped back so the top shows.
            model.transform.SetPositionAndRotation(Studio,
                viewRotation * Quaternion.AngleAxis(18f, Vector3.right) * Quaternion.AngleAxis(205f, Vector3.up));

            var bounds = new Bounds(Studio, Vector3.zero);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(renderer.bounds);

            const float fieldOfView = 30f;
            float distance = bounds.extents.magnitude / Mathf.Sin(fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;

            var go = new GameObject("Icon Camera");
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = Mathf.Max(0.01f, distance - bounds.extents.magnitude * 2f);
            camera.farClipPlane = distance + bounds.extents.magnitude * 2f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.transform.SetPositionAndRotation(bounds.center - viewRotation * Vector3.forward * distance, viewRotation);
            camera.targetTexture = texture;

            var request = new RenderPipeline.StandardRequest { destination = texture };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
                camera.Render();

            camera.targetTexture = null;
            // Hide at once: Destroy waits for the end of the frame, and the next icon is shot in the same spot.
            model.SetActive(false);
            Object.Destroy(go);
            Object.Destroy(model);
            return texture;
        }
    }
}
