using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LiminalLabs.Atlas.SampleM0
{
    /// <summary>
    /// Demo-local input polling that works on either input backend.
    ///
    /// The legacy <c>UnityEngine.Input</c> class throws outright in a project switched to
    /// the Input System package, so a sample that reads it directly does not merely feel
    /// wrong on a modern project - it fails on the first frame. Which backend is active is
    /// a Player Settings dropdown belonging to whoever imports this, not something the
    /// package gets to decide.
    ///
    /// Atlas itself reads no input at all. This exists only so the demo can be turned
    /// around, because a bearing is only interesting once the viewer rotates.
    /// </summary>
    internal static class AtlasM0Input
    {
#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// The Input System reports raw pointer delta in pixels, while the legacy Mouse X
        /// and Mouse Y axes arrive already scaled by the Input Manager's default 0.1
        /// sensitivity. Matching that here is what keeps a single lookSensitivity correct
        /// on both backends, instead of a turn rate that differs by an order of magnitude
        /// depending on a setting the sample cannot see.
        /// </summary>
        private const float LegacyAxisScale = 0.1f;

        public static bool ReadoutPressed =>
            Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;

        public static bool LookHeld =>
            Mouse.current != null && Mouse.current.rightButton.isPressed;

        public static Vector2 LookDelta => Mouse.current == null
            ? Vector2.zero
            : Mouse.current.delta.ReadValue() * LegacyAxisScale;
#else
        public static bool ReadoutPressed => Input.GetKeyDown(KeyCode.Tab);

        public static bool LookHeld => Input.GetMouseButton(1);

        public static Vector2 LookDelta =>
            new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
    }
}
