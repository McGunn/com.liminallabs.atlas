using LiminalLabs.Save;
using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// M5: persists what has been discovered.
    ///
    /// Compiles only when <c>com.liminallabs.save</c> is installed — the assembly is gated
    /// on <c>LIMINAL_SAVE</c>, so Atlas never depends on the save package and a project
    /// without it never sees this exist. That is the house rule for optional packages,
    /// applied between two Liminal Labs systems rather than to a Unity one.
    ///
    /// <b>What is saved is the reveal mask, and nothing else.</b> Markers are not: they are
    /// registered by whatever owns them — a quest system, a spawner, a save participant of
    /// its own — and a marker restored by the atlas would be a second copy of state the
    /// game already owns, fighting the first. Discovery is different, because it is state
    /// the atlas is the only owner of.
    ///
    /// One participant per space, so a game with an interior and an exterior gets two
    /// records and can lose one without corrupting the other.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Atlas/Atlas Save Bridge")]
    [DefaultExecutionOrder(70)]   // after the space registers and the mask is built
    public sealed class AtlasSaveBridge : MonoBehaviour, ISaveParticipant
    {
        [Header("Space")]
        [Tooltip("Leave empty to use the space on this object, then the viewer's space.")]
        [SerializeField] private AtlasSpaceBehaviour space;

        [Tooltip("Leave empty to search this object's parents, then the scene.")]
        [SerializeField] private AtlasRegistryBehaviour registry;

        [Header("Record")]
        [Tooltip("Stable across builds and renames. Changing it orphans existing saves.")]
        [SerializeField] private string saveKey = "atlas.discovery";

        [SerializeField] private SaveScope scope = SaveScope.Slot;

        private AtlasSpace target;

        public string SaveKey =>
            target != null && !string.IsNullOrEmpty(target.Name) && target.Name != "Default"
                ? saveKey + "." + target.Name
                : saveKey;

        /// <summary>
        /// Bumped when the record's layout changes.
        ///
        /// Version 1 stores the mask's dimensions beside its bits, which is what lets a
        /// restore refuse a mask from a differently sized map rather than reveal the wrong
        /// parts of the world.
        /// </summary>
        public int SaveVersion => 1;

        private void OnEnable()
        {
            if (space == null) space = GetComponent<AtlasSpaceBehaviour>();
            if (registry == null) registry = AtlasRegistryBehaviour.ResolveFor(this);

            if (registry == null)
            {
                Debug.LogWarning(
                    $"[Atlas] '{name}' found no AtlasRegistry, so discovery will not be saved.", this);
                return;
            }

            AtlasSpaceId id = space != null ? space.Id : registry.Space;
            target = registry.Registry.Spaces.GetOrDefault(id);

            SaveParticipants.Register(this, scope);
        }

        private void OnDisable() => SaveParticipants.Unregister(this);

        public void Capture(SaveWriter writer)
        {
            AtlasReveal reveal = target?.Reveal;

            // Width zero is the marker for "this space had no mask", which restores as
            // "leave whatever is there alone" rather than as an empty mask - saving before
            // discovery is set up should not wipe it on load.
            writer.Write("width", reveal?.Width ?? 0);
            writer.Write("height", reveal?.Height ?? 0);
            writer.Write("cells", reveal?.Cells ?? System.Array.Empty<byte>());
        }

        public void Restore(SaveReader reader, RestoreContext context)
        {
            if (target == null) return;

            int width = reader.ReadInt("width");
            int height = reader.ReadInt("height");
            if (width <= 0 || height <= 0) return;

            byte[] cells = reader.ReadBytes("cells");

            if (target.Reveal == null) target.Reveal = new AtlasReveal(width, height);

            if (!target.Reveal.Restore(width, height, cells))
            {
                // Refused rather than forced. A mask that does not fit is a save from a map
                // that has since changed size, and stretching it would reveal the wrong
                // places - which reads as a corrupt save rather than as a changed map.
                Debug.LogWarning(
                    $"[Atlas] Saved discovery for '{target.Name}' is {width}x{height}, which " +
                    $"does not match this map. It has been ignored rather than misapplied.", this);
            }
        }
    }
}
