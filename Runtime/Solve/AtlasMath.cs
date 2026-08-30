using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// Where a thing is, relative to a viewer. A pure function, and that is the whole
    /// point of the file.
    ///
    /// <b>Nothing here references <c>Camera</c>, and nothing here ever will.</b> The
    /// viewer arrives as a plain struct captured once per frame, so every question this
    /// package answers can be asked with no scene, no camera and no rendered frame.
    /// Screen-space maths is fiddly enough that it has to be testable in isolation; the
    /// moment a bearing test needs a live camera, the interesting cases stop being
    /// tested and the behind-the-viewer bug ships.
    ///
    /// <b>Sign convention: negative is left, positive is right, 0 dead ahead, ±180
    /// directly behind.</b> Derived from the viewer's <c>Right</c> so it survives pitch.
    /// It is picked once and never mixed - a mixed convention produces a compass that
    /// works perfectly until the player turns around.
    /// </summary>
    public static class AtlasMath
    {
        private const float Epsilon = 1e-6f;

        /// <summary>
        /// Signed horizontal angle from the viewer's facing to a target, in degrees.
        ///
        /// Horizontal means "in the plane the viewer's <c>Up</c> defines", which is why
        /// pitching the camera does not swing the compass and why a target directly
        /// overhead does not either. Both are flattened before the angle is taken.
        /// </summary>
        /// <summary>
        /// Signed degrees from the viewer's facing to a world <b>direction</b>, rather
        /// than to a position. Negative is left, matching <see cref="Bearing"/>.
        ///
        /// This is what puts N, E, S and W on a compass bar: they are directions with no
        /// position, and computing them by inventing a point far away is both wrong near
        /// the world origin and needless. Sharing the gimbal handling with
        /// <see cref="Bearing"/> is the point - a compass whose markers and whose cardinal
        /// letters disagreed while the player looked up would be worse than one with no
        /// letters at all.
        /// </summary>
        public static float BearingOfDirection(in AtlasViewer viewer, Vector3 direction)
        {
            Vector3 up = viewer.Up;

            Vector3 flat = Vector3.ProjectOnPlane(direction, up);
            if (flat.sqrMagnitude < Epsilon) return 0f;

            Vector3 forward = Vector3.ProjectOnPlane(viewer.Forward, up);
            if (forward.sqrMagnitude < Epsilon) forward = Vector3.Cross(viewer.Right, up);
            if (forward.sqrMagnitude < Epsilon) return 0f;

            float angle = Vector3.Angle(forward, flat);
            return Vector3.Dot(flat, viewer.Right) < 0f ? -angle : angle;
        }

        public static float Bearing(in AtlasViewer viewer, Vector3 target)
        {
            Vector3 up = viewer.Up;

            Vector3 toTarget = Vector3.ProjectOnPlane(target - viewer.Position, up);
            if (toTarget.sqrMagnitude < Epsilon) return 0f;   // directly above or below

            // Looking straight up or down flattens Forward to nothing, which is the
            // gimbal case every hand-rolled compass spins wildly in. Right is still
            // meaningful there, so the facing is rebuilt from it.
            Vector3 forward = Vector3.ProjectOnPlane(viewer.Forward, up);
            if (forward.sqrMagnitude < Epsilon) forward = Vector3.Cross(viewer.Right, up);
            if (forward.sqrMagnitude < Epsilon) return 0f;

            float angle = Vector3.Angle(forward, toTarget);
            return Vector3.Dot(toTarget, viewer.Right) < 0f ? -angle : angle;
        }

        /// <summary>
        /// Viewport position, matching <c>Camera.WorldToViewportPoint</c>: x and y in
        /// 0..1 across the screen, z the distance along the viewer's forward axis.
        ///
        /// z is a real distance rather than a clip depth because that is what callers
        /// want and what makes <c>z &lt; 0</c> mean the honest thing: behind you.
        /// </summary>
        public static Vector3 Viewport(in AtlasViewer viewer, Vector3 target)
        {
            float depth = Vector3.Dot(target - viewer.Position, viewer.Forward);

            Vector4 clip = viewer.WorldToViewport * new Vector4(target.x, target.y, target.z, 1f);

            // On the camera plane w is zero and the divide is undefined. Nudging it is
            // better than propagating an infinity that turns into a NaN two frames later
            // in someone else's layout code.
            float w = clip.w;
            if (w > -Epsilon && w < Epsilon) w = w < 0f ? -Epsilon : Epsilon;

            return new Vector3(
                clip.x / w * 0.5f + 0.5f,
                clip.y / w * 0.5f + 0.5f,
                depth);
        }

        /// <summary>Whether a viewport point is on screen and in front.</summary>
        public static bool IsOnScreen(Vector3 viewportPoint, float margin = 0f) =>
            viewportPoint.z > 0f &&
            viewportPoint.x >= margin && viewportPoint.x <= 1f - margin &&
            viewportPoint.y >= margin && viewportPoint.y <= 1f - margin;

        /// <summary>
        /// Where an off-screen indicator goes, and which way its arrow points.
        ///
        /// This holds the bug the package exists to stop shipping. A projection matrix
        /// divides by w, and behind the viewer w is negative - so the projected point
        /// comes back <b>mirrored through the centre</b>. Something behind and to your
        /// left projects to the right of the screen, and an indicator that clamps the
        /// raw value pins to the wrong edge with its arrow pointing exactly backwards.
        /// It looks almost right, which is why it survives playtests.
        ///
        /// So: mirror first, then clamp. A point that is already comfortably on screen
        /// is returned untouched; anything else is pushed out to the border.
        /// </summary>
        /// <param name="viewportPoint">From <see cref="Viewport"/>; z &lt; 0 means behind.</param>
        /// <param name="margin">Inset from the viewport edge, 0..0.5.</param>
        /// <param name="angle">Degrees to rotate the arrow by, 0 pointing right.</param>
        public static Vector2 ClampToEdge(Vector3 viewportPoint, float margin, out float angle)
        {
            margin = Mathf.Clamp(margin, 0f, 0.49f);

            var point = new Vector2(viewportPoint.x, viewportPoint.y);
            bool behind = viewportPoint.z < 0f;

            if (behind) point = new Vector2(1f - point.x, 1f - point.y);

            var centre = new Vector2(0.5f, 0.5f);
            Vector2 direction = point - centre;

            // Behind and dead centre: the mirror leaves no direction to work with, and
            // an indicator has to go somewhere. Straight down reads as "behind you",
            // which is the truth.
            if (direction.sqrMagnitude < Epsilon * Epsilon) direction = Vector2.down;

            angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            float half = 0.5f - margin;
            bool inside = !behind &&
                          Mathf.Abs(direction.x) <= half && Mathf.Abs(direction.y) <= half;
            if (inside) return point;

            // Scale the direction until it meets the border rectangle: whichever axis
            // reaches its limit first decides.
            float scaleX = half / Mathf.Max(Mathf.Abs(direction.x), Epsilon);
            float scaleY = half / Mathf.Max(Mathf.Abs(direction.y), Epsilon);

            return centre + direction * Mathf.Min(scaleX, scaleY);
        }

        /// <summary>
        /// Alpha from distance, 0 at the limit and 1 comfortably inside it.
        ///
        /// A marker that vanishes the instant it crosses <c>MaxDistance</c> pops; one
        /// that fades over the last stretch does not. No limit means no fade, which is
        /// what a marker with <c>MaxDistance</c> of zero is asking for.
        /// </summary>
        public static float Fade(float distance, float maxDistance, float fadeFraction = 0.2f)
        {
            if (maxDistance <= 0f) return 1f;
            if (distance >= maxDistance) return 0f;

            float band = maxDistance * Mathf.Clamp01(fadeFraction);
            if (band <= Epsilon) return 1f;

            float into = maxDistance - distance;
            return into >= band ? 1f : Mathf.Clamp01(into / band);
        }

        /// <summary>
        /// How busy the viewer is this frame, 0 to 1, from how fast it moved and turned.
        ///
        /// A HUD that dims while you stand still and returns the moment you move is the
        /// difference between a compass you forget about and one you resent. The measure
        /// belongs here rather than in a presenter because it is arithmetic over two
        /// frozen viewers and nothing else - so it is testable with no scene, and both
        /// views fade on the same signal instead of each guessing.
        ///
        /// Turning counts as much as walking, deliberately: a player sweeping the camera
        /// is looking for something, which is exactly when a compass earns its space.
        /// </summary>
        public static float Activity(in AtlasViewer previous, in AtlasViewer current,
                                     float deltaTime,
                                     float moveSpeedForFull = 1.5f,
                                     float turnSpeedForFull = 40f)
        {
            if (deltaTime <= Epsilon) return 0f;

            float moved = (current.Position - previous.Position).magnitude / deltaTime;
            float turned = Vector3.Angle(previous.Forward, current.Forward) / deltaTime;

            float fromMove = moveSpeedForFull > Epsilon ? moved / moveSpeedForFull : 0f;
            float fromTurn = turnSpeedForFull > Epsilon ? turned / turnSpeedForFull : 0f;

            return Mathf.Clamp01(Mathf.Max(fromMove, fromTurn));
        }

        /// <summary>
        /// Scale for a marker at a given fade, between a near and a far size.
        ///
        /// Small enough to read as depth, never small enough to be unclickable or
        /// illegible - which is why it interpolates between two authored sizes rather
        /// than multiplying by distance.
        /// </summary>
        public static float DistanceScale(float fade, float minScale, float maxScale) =>
            Mathf.LerpUnclamped(minScale, maxScale, Mathf.Clamp01(fade));
    }
}
