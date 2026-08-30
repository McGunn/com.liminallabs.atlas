using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// A map plane with a world transform.
    ///
    /// A map is not a texture - that is the idea the whole package is built on. Modelled
    /// as a plane with a transform, an interior, a basement, a tower and a world map are
    /// the same type with different numbers, and the minimap and the world map become
    /// one projection with two framings rather than two systems.
    ///
    /// M0 uses almost none of this: it registers a Default space and excludes markers
    /// that are somewhere else. The fields are here because a space model cannot be
    /// retrofitted onto a system that assumed one plane everywhere, and because
    /// <see cref="AtlasSpaceId"/> is already in saved data.
    /// </summary>
    public sealed class AtlasSpace
    {
        public AtlasSpaceId Id;

        /// <summary>The world volume this plane covers. Used by baking (M3) and by map
        /// framing (M1).</summary>
        public Bounds WorldBounds;

        /// <summary>World to map plane. XZ by default; XY for a side-scroller; anything
        /// at all for a stylised map that is not a projection of the world.</summary>
        public Matrix4x4 WorldToMap = XZ;

        /// <summary>Baked or authored map image. Null in M0 - baking is M3.</summary>
        public Texture Image;

        /// <summary>Where this floor sits and how thick it is, for deciding which floor
        /// a position belongs to. Unused in M0.</summary>
        public float FloorHeight;

        public float FloorThickness = 3f;

        public string Name = "Default";

        /// <summary>
        /// What of this space has been seen. Null means no fog, which is what a game that
        /// never asks for discovery gets.
        ///
        /// Lives on the space rather than in a separate system because it is indexed
        /// against the same bounds as the baked image and the map framing - three things
        /// over one extent, which is the whole argument for modelling a map as a plane.
        /// </summary>
        public AtlasReveal Reveal;

        /// <summary>
        /// Where a world position falls inside the bounds, 0..1 from the minimum corner.
        ///
        /// The index a reveal mask and a baked image share. Not the same as
        /// <see cref="ToMap"/>, which is unbounded plane coordinates - conflating them is
        /// how fog ends up offset from the terrain it is meant to hide.
        /// </summary>
        public Vector2 Normalise(Vector3 world)
        {
            Vector3 size = WorldBounds.size;
            Vector3 min = WorldBounds.min;

            return new Vector2(
                Mathf.Approximately(size.x, 0f) ? 0.5f : (world.x - min.x) / size.x,
                Mathf.Approximately(size.z, 0f) ? 0.5f : (world.z - min.z) / size.z);
        }

        /// <summary>Whether a world position has been seen. True when there is no mask,
        /// so a game with no fog behaves as though everything is revealed rather than as
        /// though nothing is.</summary>
        public bool IsRevealed(Vector3 world) =>
            Reveal == null || Reveal.IsRevealedAt(Normalise(world));

        /// <summary>
        /// Top-down: world XZ becomes map XY.
        ///
        /// Written out rather than built with <c>Matrix4x4.TRS</c> because this is a
        /// default on a type that tests construct with no engine running, and TRS is
        /// native-backed.
        /// </summary>
        public static Matrix4x4 XZ
        {
            get
            {
                var m = Matrix4x4.identity;
                m.m00 = 1f; m.m01 = 0f; m.m02 = 0f;
                m.m10 = 0f; m.m11 = 0f; m.m12 = 1f;   // world z becomes map y
                m.m20 = 0f; m.m21 = 0f; m.m22 = 0f;
                return m;
            }
        }

        /// <summary>Where a world position lands on this plane.</summary>
        public Vector2 ToMap(Vector3 world)
        {
            Vector3 mapped = WorldToMap.MultiplyPoint(world);
            return new Vector2(mapped.x, mapped.y);
        }

        public override string ToString() => $"{Name} ({Id})";
    }
}
