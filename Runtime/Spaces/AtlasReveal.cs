using System;
using UnityEngine;

namespace LiminalLabs.Atlas
{
    /// <summary>
    /// M4: what of a space has been seen.
    ///
    /// A grid of bits over the space's bounds, one per cell, set as the viewer moves. The
    /// design put discovery behind the space model rather than beside it for the same
    /// reason as baking: a space is already a plane with a known extent, so "what has been
    /// revealed" is an index over something the system already has rather than a new thing
    /// to author and keep aligned.
    ///
    /// <b>Bits, not floats.</b> A 256×256 mask is 8 KB and serialises to about a kilobyte
    /// compressed, which is a size a save file can carry per space without anyone
    /// negotiating. Soft edges are the presenter's business — a shader sampling this can
    /// blur it as much as it likes, and the data stays exact.
    ///
    /// Pure: no Unity object, no engine call beyond <c>Mathf</c>, so the whole of discovery
    /// is testable with no scene.
    /// </summary>
    [Serializable]
    public sealed class AtlasReveal
    {
        [SerializeField] private int width;
        [SerializeField] private int height;
        [SerializeField] private byte[] cells;

        /// <summary>
        /// Bumped whenever anything is revealed or cleared.
        ///
        /// So a renderer can skip rebuilding its texture when nothing changed - which is
        /// most frames, since the mask is filled in on a timer and not at all while the
        /// viewer stands still. Comparing a counter is the difference between fog costing
        /// nothing and fog costing a texture upload every frame.
        ///
        /// Not serialised: it is a change signal, not state, and a restored mask should
        /// look new to whatever is drawing it.
        /// </summary>
        [NonSerialized] public int Version;

        /// <summary>Cells across the bounds' X.</summary>
        public int Width => width;

        /// <summary>Cells across the bounds' Z.</summary>
        public int Height => height;

        /// <summary>The raw bits, for a save codec. One bit per cell, row-major from the
        /// bounds' minimum corner.</summary>
        public byte[] Cells => cells;

        public AtlasReveal(int width, int height)
        {
            this.width = Mathf.Max(1, width);
            this.height = Mathf.Max(1, height);
            cells = new byte[(this.width * this.height + 7) / 8];
        }

        /// <summary>Rebuilds from saved bits. Returns false if the array is the wrong size
        /// for the dimensions, which is what a save from an older map layout looks like -
        /// and silently accepting it would reveal the wrong parts of the world.</summary>
        public bool Restore(int savedWidth, int savedHeight, byte[] savedCells)
        {
            if (savedCells == null) return false;
            if (savedWidth <= 0 || savedHeight <= 0) return false;
            if (savedCells.Length != (savedWidth * savedHeight + 7) / 8) return false;

            width = savedWidth;
            height = savedHeight;
            cells = savedCells;
            Version++;
            return true;
        }

        /// <summary>Everything hidden again.</summary>
        public void Clear()
        {
            Array.Clear(cells, 0, cells.Length);
            Version++;
        }

        /// <summary>Everything revealed. For a debug command, and for a game that wants
        /// fog off without removing the mask.</summary>
        public void RevealAll()
        {
            for (int i = 0; i < cells.Length; i++) cells[i] = 0xFF;
            Version++;
        }

        /// <summary>Whether a cell has been seen. Out-of-range reads as revealed, because
        /// outside the map is not somewhere anyone needs to explore.</summary>
        public bool IsRevealed(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return true;

            int index = y * width + x;
            return (cells[index >> 3] & (1 << (index & 7))) != 0;
        }

        /// <summary>Whether a normalised point on the plane has been seen. (0,0) is the
        /// bounds' minimum corner, (1,1) its maximum.</summary>
        public bool IsRevealedAt(Vector2 normalised) =>
            IsRevealed(Mathf.FloorToInt(normalised.x * width),
                       Mathf.FloorToInt(normalised.y * height));

        /// <summary>
        /// Reveals a disc, in normalised bounds coordinates.
        ///
        /// A disc rather than the cell the viewer is in: revealing one cell at a time makes
        /// a trail the width of the player, which is only correct for a game about crawling
        /// through tunnels. The radius is the sight distance divided by the bounds, so a
        /// caller works in world units and this works in cells.
        ///
        /// Returns how many cells this call newly revealed, which is what a game hooks to
        /// award exploration without diffing the whole mask.
        /// </summary>
        public int Reveal(Vector2 centre, float radius)
        {
            if (radius <= 0f) return 0;

            float cellRadiusX = radius * width;
            float cellRadiusY = radius * height;

            int centreX = Mathf.FloorToInt(centre.x * width);
            int centreY = Mathf.FloorToInt(centre.y * height);

            int minX = Mathf.Max(0, Mathf.FloorToInt(centreX - cellRadiusX));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(centreX + cellRadiusX));
            int minY = Mathf.Max(0, Mathf.FloorToInt(centreY - cellRadiusY));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(centreY + cellRadiusY));

            int revealed = 0;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    // Elliptical in cell space, which is circular in world space whenever
                    // the mask's aspect matches the bounds' - and it does, because the
                    // caller sizes it from them.
                    float dx = (x - centreX) / Mathf.Max(cellRadiusX, 0.0001f);
                    float dy = (y - centreY) / Mathf.Max(cellRadiusY, 0.0001f);
                    if (dx * dx + dy * dy > 1f) continue;

                    int index = y * width + x;
                    int mask = 1 << (index & 7);
                    if ((cells[index >> 3] & mask) != 0) continue;

                    cells[index >> 3] |= (byte)mask;
                    revealed++;
                }
            }

            if (revealed > 0) Version++;
            return revealed;
        }

        /// <summary>How much of the space has been seen, 0 to 1. What a completion
        /// readout shows, and cheap enough to call on a timer rather than per frame.</summary>
        public float RevealedFraction()
        {
            int total = width * height;
            if (total <= 0) return 1f;

            int seen = 0;
            for (int i = 0; i < total; i++)
                if ((cells[i >> 3] & (1 << (i & 7))) != 0) seen++;

            return seen / (float)total;
        }
    }
}
