using DecoreXR.Core;
using UnityEngine;

namespace DecoreXR.Painting
{
    /// <summary>
    /// Paints one whole surface a single solid colour — the first tool, and the one the MVP's
    /// vertical slice is built to prove end to end.
    /// </summary>
    /// <remarks>
    /// Immutable, like every command: the history keeps it for the session and re-renders from it
    /// (ADR 0007), so it must still mean the same thing later. Being the whole-surface case it needs
    /// no <c>(u,v)</c> extent at all, which is why it is the simplest possible implementation of
    /// ADR 0003's "each tool is a command type".
    /// </remarks>
    public sealed class FillCommand : IPaintCommand
    {
        private readonly string surfaceId;
        private readonly Color32 fillColor;

        public FillCommand(string surfaceId, Color32 fillColor)
        {
            this.surfaceId = surfaceId;
            this.fillColor = fillColor;
        }

        /// <inheritdoc />
        public string SurfaceId => surfaceId;

        /// <inheritdoc />
        public string DisplayName => "Fill";

        /// <summary>The colour this command paints the surface.</summary>
        public Color32 FillColor => fillColor;

        /// <inheritdoc />
        public void Render(IPaintCanvas canvas)
        {
            canvas.FillAll(fillColor);
        }
    }
}
