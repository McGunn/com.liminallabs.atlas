using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// Turns an icon id into something to draw.
    ///
    /// A seam rather than a field on the marker, so the package never learns what an
    /// asset reference is. A project resolves from a sprite array, from Resources, from
    /// Addressables or from its own content system, and the atlas depends on none of
    /// them - which is the difference between a HUD package that drops into a project
    /// and one that brings a package manager argument with it.
    /// </summary>
    public interface IAtlasIconProvider
    {
        /// <summary>The sprite for an id, or null. Null is drawn as nothing, not as an
        /// error - a missing icon should cost a blank marker, not a broken frame.</summary>
        Sprite Resolve(int iconId);
    }

    /// <summary>
    /// The simplest provider that works: an array, indexed by id.
    ///
    /// An asset rather than a component so several presenters share one set without
    /// being on the same object, and so the ids in a scene and the ids in a save agree
    /// on what icon 3 is.
    /// </summary>
    [CreateAssetMenu(menuName = "Liminal Labs/Atlas/Sprite Icons", fileName = "AtlasIcons")]
    public sealed class AtlasSpriteIcons : ScriptableObject, IAtlasIconProvider
    {
        [SerializeField, Tooltip("Indexed by IconId. Order is content - inserting in the middle renumbers every marker after it.")]
        private List<Sprite> icons = new List<Sprite>();

        public Sprite Resolve(int iconId) =>
            iconId >= 0 && iconId < icons.Count ? icons[iconId] : null;

        public int Count => icons.Count;
    }
}
