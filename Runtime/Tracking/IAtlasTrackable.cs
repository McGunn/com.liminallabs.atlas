using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// Something the atlas can point at.
    ///
    /// <c>Position</c> is a value rather than a <c>Transform</c>, and that is the load
    /// bearing decision: it is what lets a quest, a network peer, a content instance or
    /// a unit in a simulation be tracked without ever becoming a GameObject.
    ///
    /// No base class, here or anywhere else in the public API. A package that requires
    /// inheritance is a package a project has to be built around.
    /// </summary>
    public interface IAtlasTrackable
    {
        Vector3 Position { get; }

        AtlasMarker Marker { get; }

        AtlasSpaceId Space { get; }

        /// <summary>Off means "skip me this frame" - cheaper and less error-prone than
        /// unregistering and re-registering something that comes and goes.</summary>
        bool IsTracked { get; }
    }
}
