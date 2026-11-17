using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TinyPlanet.UI
{
    /// <summary>
    /// Makes a button feel pressable: it shrinks a little under the finger and springs back on release.
    /// Every click is also reported through <see cref="Clicked"/>, so the game can answer with a sound and a tick.
    /// </summary>
    public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        public static event Action Clicked;

        private float target = 1f;
        private float scale = 1f;
        private float velocity;

        public void OnPointerDown(PointerEventData eventData) => target = 0.92f;

        public void OnPointerUp(PointerEventData eventData) => target = 1f;

        public void OnPointerExit(PointerEventData eventData) => target = 1f;

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();

        private void OnDisable()
        {
            target = scale = 1f;
            velocity = 0f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (Mathf.Approximately(scale, target) && Mathf.Abs(velocity) < 0.001f) return;
            // A springy follow, so a release overshoots a touch before settling.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            velocity += (target - scale) * 600f * dt;
            velocity *= Mathf.Exp(-dt * 22f);
            scale += velocity * dt;
            if (Mathf.Abs(target - scale) < 0.0005f && Mathf.Abs(velocity) < 0.001f)
            {
                scale = target;
                velocity = 0f;
            }
            transform.localScale = Vector3.one * scale;
        }
    }
}
