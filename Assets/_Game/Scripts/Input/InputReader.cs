using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace TinyPlanet.GameInput
{
    /// <summary>
    /// Turns touch (and mouse, for the Editor) into three gestures: drag, zoom and tap.
    /// A press that starts on the UI is ignored, and a press that moved or lasted too long is never a tap.
    /// </summary>
    public sealed class InputReader : MonoBehaviour
    {
        [SerializeField] private float tapMaxSeconds = 0.35f;
        [SerializeField] private float tapMaxInches = 0.08f;
        [SerializeField] private float scrollZoomStep = 0.1f;

        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        private bool pressActive;
        private bool pressOnUi;
        private bool pressMoved;
        private bool pressWasMultiTouch;
        private Vector2 pressStart;
        private float pressStartTime;
        private float lastPinchDistance;

        /// <summary>Drag movement in screen pixels since the last frame.</summary>
        public event Action<Vector2> Dragged;
        public event Action DragEnded;
        /// <summary>Zoom as a multiplier on viewing distance; below one means closer.</summary>
        public event Action<float> Zoomed;
        /// <summary>A quick press and release without movement, in screen pixels.</summary>
        public event Action<Vector2> Tapped;

        private float TapMaxPixels => Screen.dpi > 0f ? Screen.dpi * tapMaxInches : 12f;

        private void OnEnable() => EnhancedTouchSupport.Enable();

        private void OnDisable() => EnhancedTouchSupport.Disable();

        private void Update()
        {
            var touches = Touch.activeTouches;
            if (touches.Count > 0)
                ReadTouches(touches);
            else if (Mouse.current != null)
                ReadMouse(Mouse.current);
            else if (pressActive)
                EndPress(pressStart, allowTap: false);
        }

        private void ReadTouches(UnityEngine.InputSystem.Utilities.ReadOnlyArray<Touch> touches)
        {
            var first = touches[0];
            if (!pressActive)
                BeginPress(first.screenPosition);

            if (touches.Count >= 2)
            {
                float pinch = Vector2.Distance(first.screenPosition, touches[1].screenPosition);
                if (pressWasMultiTouch && lastPinchDistance > 1f && pinch > 1f && !pressOnUi)
                    Zoomed?.Invoke(lastPinchDistance / pinch);
                lastPinchDistance = pinch;
                pressWasMultiTouch = true;
                pressMoved = true;
                return;
            }

            lastPinchDistance = 0f;
            bool ended = first.phase == TouchPhase.Ended || first.phase == TouchPhase.Canceled;
            if (ended)
            {
                EndPress(first.screenPosition, allowTap: first.phase == TouchPhase.Ended);
                return;
            }

            // After a pinch, the finger left on screen does not drag: it would make the planet jump.
            if (!pressWasMultiTouch)
                MovePress(first.screenPosition, first.delta);
        }

        private void ReadMouse(Mouse mouse)
        {
            Vector2 position = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
                BeginPress(position);
            else if (pressActive && mouse.leftButton.isPressed)
                MovePress(position, mouse.delta.ReadValue());

            if (pressActive && mouse.leftButton.wasReleasedThisFrame)
                EndPress(position, allowTap: true);

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsOverUi(position))
                Zoomed?.Invoke(scroll > 0f ? 1f - scrollZoomStep : 1f + scrollZoomStep);
        }

        private void BeginPress(Vector2 position)
        {
            pressActive = true;
            pressOnUi = IsOverUi(position);
            pressMoved = false;
            pressWasMultiTouch = false;
            pressStart = position;
            pressStartTime = Time.unscaledTime;
            lastPinchDistance = 0f;
        }

        private void MovePress(Vector2 position, Vector2 delta)
        {
            if (pressOnUi) return;

            if (!pressMoved && Vector2.Distance(position, pressStart) > TapMaxPixels)
                pressMoved = true;

            if (pressMoved)
                Dragged?.Invoke(delta);
        }

        private void EndPress(Vector2 position, bool allowTap)
        {
            bool isTap = allowTap
                && !pressOnUi
                && !pressMoved
                && !pressWasMultiTouch
                && Time.unscaledTime - pressStartTime <= tapMaxSeconds;

            pressActive = false;
            if (!pressOnUi) DragEnded?.Invoke();
            if (isTap) Tapped?.Invoke(position);
        }

        private bool IsOverUi(Vector2 position)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            var pointer = new PointerEventData(eventSystem) { position = position };
            uiHits.Clear();
            eventSystem.RaycastAll(pointer, uiHits);
            return uiHits.Count > 0;
        }
    }
}
