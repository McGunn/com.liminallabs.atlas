using NUnit.Framework;
using UnityEngine;

namespace LiminalLabs.Atlas.Tests
{
    /// <summary>
    /// §7.3, plus the identity properties §8.7 says have to be settled in M0 - because
    /// once markers carry a space id, changing how spaces are identified is a save-data
    /// migration rather than a refactor.
    /// </summary>
    public class SpaceTests
    {
        // 18
        [Test]
        public void DefaultExistsWithoutBeingRegistered()
        {
            var registry = new AtlasRegistry();

            Assert.IsNotNull(registry.Spaces.Default);
            Assert.IsTrue(registry.Spaces.TryGet(AtlasSpaceId.Default, out AtlasSpace space));
            Assert.AreSame(registry.Spaces.Default, space);
        }

        // 19
        [Test]
        public void AMarkerWithNoSpaceIsInDefault()
        {
            // Not "the registry substitutes Default" - default(AtlasSpaceId) IS Default,
            // so there is no path by which an unassigned marker lands nowhere.
            AtlasSpaceId unassigned = default;

            Assert.IsTrue(unassigned.IsDefault);
            Assert.AreEqual(AtlasSpaceId.Default, unassigned);
        }

        [Test]
        public void TheSameNameAlwaysGivesTheSameId()
        {
            Assert.AreEqual(new AtlasSpaceId("Tower Interior"), new AtlasSpaceId("Tower Interior"));
            Assert.AreNotEqual(new AtlasSpaceId("Tower Interior"), new AtlasSpaceId("Tower Basement"));
        }

        [Test]
        public void ANamedSpaceNeverCollidesWithDefault()
        {
            // Default is zero, and a name that hashed to zero would silently merge with
            // it - every marker in that space appearing in the overworld instead.
            string[] names = { "a", "Default", "Interior", "0", "Tower", "  " };

            foreach (string name in names)
            {
                var id = new AtlasSpaceId(name);
                Assert.IsFalse(id.IsDefault, name + " collided with Default");
            }
        }

        [Test]
        public void AnUnknownIdResolvesToDefaultRatherThanNull()
        {
            var registry = new AtlasRegistry();

            // Stale saved data naming a space this build no longer has: a visible marker
            // in the wrong place beats a null reference inside a HUD.
            AtlasSpace space = registry.Spaces.GetOrDefault(new AtlasSpaceId("Deleted Region"));

            Assert.IsNotNull(space);
            Assert.AreSame(registry.Spaces.Default, space);
        }

        [Test]
        public void CreatingASpaceRegistersIt()
        {
            var registry = new AtlasRegistry();
            AtlasSpace interior = registry.Spaces.Create("Tower Interior");

            Assert.AreEqual(2, registry.Spaces.Count);
            Assert.IsTrue(registry.Spaces.TryGet(new AtlasSpaceId("Tower Interior"), out AtlasSpace found));
            Assert.AreSame(interior, found);
        }

        [Test]
        public void DefaultCannotBeRemoved()
        {
            var registry = new AtlasRegistry();

            Assert.IsFalse(registry.Spaces.Remove(AtlasSpaceId.Default),
                "removing it would turn every unassigned marker into a null lookup");
            Assert.IsNotNull(registry.Spaces.Default);
        }

        [Test]
        public void TheDefaultPlaneMapsWorldXZToMapXY()
        {
            var space = new AtlasSpace();
            Vector2 mapped = space.ToMap(new Vector3(3f, 99f, -7f));

            Assert.AreEqual(3f, mapped.x, 0.0001f);
            Assert.AreEqual(-7f, mapped.y, 0.0001f, "height is dropped, which is what top-down means");
        }
    }
}
