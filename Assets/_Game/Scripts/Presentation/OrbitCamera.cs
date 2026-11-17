using UnityEngine;

namespace TinyPlanet.Presentation
{
    /// <summary>
    /// Orbits the camera around the planet at the origin. Dragging turns it like a trackball with inertia,
    /// and it drifts slowly when left alone.
    /// </summary>
    public sealed class OrbitCamera : MonoBehaviour
    {
        [SerializeField] private float damping = 4f;
        [SerializeField] private float zoomSharpness = 12f;
        [SerializeField] private float idleDelay = 4f;
        [SerializeField] private float idleDegreesPerSecond = 3f;
        [SerializeField] private float closestInRadii = 1.7f;
        [SerializeField] private float framingMargin = 1.3f;

        private Camera cam;
        private float planetRadius;
        private Quaternion orbit = Quaternion.Euler(20f, -30f, 0f);
        private float distance;
        private float zoom = 1f; // 0 = closest, 1 = whole planet in view
        private float minDistance;
        private float maxDistance;
        private Vector2 pendingDegrees;
        private Vector2 velocity;
        private bool dragging;
        private float idleTime;

        // Zoom is kept as a fraction of the allowed range, so the framing survives a change of aspect ratio.
        private float TargetDistance => Mathf.Lerp(minDistance, maxDistance, zoom);

        public void Init(Camera camera, float radius)
        {
            cam = camera;
            planetRadius = radius;
            UpdateLimits();
            distance = TargetDistance;
            Apply();
        }

        /// <summary>Turns the planet under the finger by a drag given in screen pixels.</summary>
        public void Drag(Vector2 pixels)
        {
            // Convert pixels to degrees so the surface roughly follows the finger at any zoom level.
            float worldPerPixel = 2f * (distance - planetRadius) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            pendingDegrees += pixels * (worldPerPixel / planetRadius * Mathf.Rad2Deg);
            dragging = true;
        }

        public void EndDrag() => dragging = false;

        /// <summary>Multiplies the viewing distance; values below one move closer.</summary>
        public void Zoom(float factor)
        {
            zoom = Mathf.InverseLerp(minDistance, maxDistance, TargetDistance * factor);
            idleTime = 0f;
        }

        private void LateUpdate()
        {
            if (cam == null) return;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            UpdateLimits();

            if (dragging)
            {
                Turn(pendingDegrees);
                velocity = Vector2.Lerp(velocity, pendingDegrees / dt, 0.5f);
                pendingDegrees = Vector2.zero;
                idleTime = 0f;
            }
            else
            {
                Turn(velocity * dt);
                velocity *= Mathf.Exp(-damping * dt);
                idleTime += dt;
                if (idleTime > idleDelay)
                    Turn(new Vector2(idleDegreesPerSecond * dt, 0f));
            }

            distance = Mathf.Lerp(distance, TargetDistance,1f - Mathf.Exp(-zoomSharpness * dt));
            Apply();
        }

        private void Turn(Vector2 degrees)
        {
            if (degrees == Vector2.zero) return;
            orbit = (orbit * Quaternion.Euler(-degrees.y, degrees.x, 0f)).normalized;
        }

        private void Apply()
        {
            transform.SetPositionAndRotation(orbit * new Vector3(0f, 0f, -distance), orbit);
        }

        // The farthest view frames the whole planet on the narrower screen axis, whatever the aspect ratio.
        private void UpdateLimits()
        {
            float halfVertical = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * cam.aspect);
            maxDistance = planetRadius * framingMargin / Mathf.Sin(Mathf.Min(halfVertical, halfHorizontal));
            minDistance = Mathf.Min(planetRadius * closestInRadii, maxDistance);
        }
    }
}
