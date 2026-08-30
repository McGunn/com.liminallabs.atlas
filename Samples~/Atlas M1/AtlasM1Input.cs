using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LiminalLabs.Atlas.SampleM1
{
    /// <summary>
    /// Demo-local input polling that works on either input backend.
    ///
    /// The legacy <c>UnityEngine.Input</c> class throws outright in a project switched to
    /// the Input System package, so a sample that reads it directly fails on its first
    /// frame. Which backend is active belongs to whoever imports this.
    /// </summary>
    internal static class AtlasM1Input
    {
#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// The Input System reports raw pointer delta in pixels, while the legacy Mouse X
        /// and Mouse Y axes arrive already scaled by the Input Manager's default 0.1
        /// sensitivity. Matching that keeps one lookSensitivity correct on both backends.
        /// </summary>
        private const float LegacyAxisScale = 0.1f;

        public static bool ToggleMapPressed =>
            Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;

        public static bool LookHeld =>
            Mouse.current != null && Mouse.current.rightButton.isPressed;

        public static Vector2 LookDelta => Mouse.current == null
            ? Vector2.zero
            : Mouse.current.delta.ReadValue() * LegacyAxisScale;

        public static Vector2 Move
        {
            get
            {
                Keyboard k = Keyboard.current;
                if (k == null) return Vector2.zero;

                float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f)
                        - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
                float y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f)
                        - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);

                return new Vector2(x, y);
            }
        }

        public static bool DragHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;

        public static Vector2 DragDelta => Mouse.current == null
            ? Vector2.zero
            : Mouse.current.delta.ReadValue();

        /// <summary>One notch of the wheel, normalised. The Input System reports 120 per
        /// notch on Windows and 1 elsewhere, so it is divided down to something a zoom
        /// step can be multiplied by on either.</summary>
        public static float ScrollNotches
        {
            get
            {
                if (Mouse.current == null) return 0f;
                float raw = Mouse.current.scroll.ReadValue().y;
                return Mathf.Abs(raw) >= 100f ? raw / 120f : raw;
            }
        }

        public static bool ResetPressed =>
            Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;

        public static bool DragPressed =>
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        public static bool DragReleased =>
            Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;

        public static Vector2 PointerPosition => Mouse.current == null
            ? Vector2.zero
            : Mouse.current.position.ReadValue();

        public static bool ClearPressed =>
            Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
#else
        public static bool ToggleMapPressed => Input.GetKeyDown(KeyCode.M);

        public static bool LookHeld => Input.GetMouseButton(1);

        public static Vector2 LookDelta =>
            new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        public static Vector2 Move =>
            new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        public static bool DragHeld => Input.GetMouseButton(0);

        public static Vector2 DragDelta =>
            new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;

        public static float ScrollNotches => Input.GetAxis("Mouse ScrollWheel") * 10f;

        public static bool ResetPressed => Input.GetKeyDown(KeyCode.R);

        public static bool DragPressed => Input.GetMouseButtonDown(0);

        public static bool DragReleased => Input.GetMouseButtonUp(0);

        public static Vector2 PointerPosition => Input.mousePosition;

        public static bool ClearPressed => Input.GetKeyDown(KeyCode.C);
#endif
    }
}
