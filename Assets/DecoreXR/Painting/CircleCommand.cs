using DecoreXR.Core;
using UnityEngine;

namespace DecoreXR.Painting
{
    /// <summary>
    /// Paints a filled circle at a point on one surface — the first tool that paints somewhere
    /// rather than everywhere, and so the first that has to carry where the user aimed.
    /// </summary>
    /// <remarks>
    /// Centre in normalized <c>(u,v)</c> and radius in metres, which is the pairing ADR 0003's
    /// resolution-independence needs: both survive the canvas being rebuilt at a different texel
    /// density (ADR 0004), because neither is expressed in texels. Mixing the two units is
    /// deliberate rather than sloppy — <c>(u,v)</c> is what a raycast hit reports and is the only
    /// frame a wall of unknown size has, while a radius in <c>(u,v)</c> would mean a different real
    /// size on each axis and draw an ellipse on any wall that is not square.
    /// <para>
    /// Immutable, like every command: the history keeps it for the session and re-renders from it
    /// (ADR 0007), so it must still mean the same thing later.
    /// </para>
    /// </remarks>
    public sealed class CircleCommand : IPaintCommand
    {
        private readonly string surfaceId;
        private readonly Vector2 center;
        private readonly float radius;
        private readonly Color32 circleColor;

        /// <param name="surfaceId">The surface's anchor id, as <see cref="IPaintCommand.SurfaceId"/>.</param>
        /// <param name="center">Centre in the surface's normalized <c>(u,v)</c>.</param>
        /// <param name="radius">Radius in metres.</param>
        /// <param name="circleColor">The colour to paint.</param>
        public CircleCommand(string surfaceId, Vector2 center, float radius, Color32 circleColor)
        {
            this.surfaceId = surfaceId;
            this.center = center;
            this.radius = radius;
            this.circleColor = circleColor;
        }

        /// <inheritdoc />
        public string SurfaceId => surfaceId;

        /// <summary>Centre of the circle in the surface's normalized <c>(u,v)</c>.</summary>
        public Vector2 Center => center;

        /// <summary>Radius of the circle in metres.</summary>
        public float Radius => radius;

        /// <summary>The colour this command paints.</summary>
        public Color32 CircleColor => circleColor;

        /// <inheritdoc />
        public void Render(IPaintCanvas canvas)
        {
            canvas.FillCircle(center, radius, circleColor);
        }
    }
}
