using System.Collections.Generic;
using UnityEngine;

namespace DecoreXR.Core
{
    /// <summary>
    /// What a command draws onto: one surface's paint canvas, addressed in that surface's own
    /// normalized <c>(u,v)</c> space (ADR 0003). <c>Painting</c> implements it over the per-wall
    /// texture.
    /// </summary>
    /// <remarks>
    /// Deliberately a drawing API and not a texture. A tool asks for the shape it means and the
    /// canvas works out how many texels that is, which keeps the <c>(u,v)</c>→texel mapping and the
    /// resolution the quality budget picked (ADR 0004) in one place instead of once per command —
    /// and keeps commands free of any Unity texture type they would then have to be persisted
    /// around (ADR 0006).
    /// <para>
    /// Each new tool adds the one operation it needs here. <see cref="FillAll"/> is M3's whole-wall
    /// paint, <see cref="FillCircle"/> is M5's shape, and <see cref="StrokePolyline"/> is M6's
    /// freehand brush.
    /// </para>
    /// </remarks>
    public interface IPaintCanvas
    {
        /// <summary>Paints the entire surface one solid colour.</summary>
        void FillAll(Color32 color);

        /// <summary>
        /// Paints a filled circle centred on a point of the surface.
        /// </summary>
        /// <param name="center">
        /// Centre in the surface's normalized <c>(u,v)</c>, the same space a
        /// <c>SurfaceHit</c> reports.
        /// </param>
        /// <param name="radius">
        /// Radius in <em>metres</em>, not in <c>(u,v)</c>. A wall is any shape it likes, so a
        /// radius expressed in normalized units would be a different real size on each axis and
        /// the "circle" would come out an ellipse on every wall that is not square. Metres is
        /// also what a size gesture naturally measures (M5-T4). Non-positive radii paint nothing.
        /// </param>
        /// <param name="color">The colour to paint.</param>
        /// <remarks>
        /// The circle is clipped to the surface, so one drawn near an edge is simply cut off by
        /// the wall rather than being rejected or wrapped round to the far side.
        /// </remarks>
        void FillCircle(Vector2 center, float radius, Color32 color);

        /// <summary>
        /// Paints a stroke of constant width along a polyline across the surface — the freehand
        /// brush.
        /// </summary>
        /// <param name="points">
        /// The stroke's path in the surface's normalized <c>(u,v)</c>, in the order it was drawn.
        /// The canvas interpolates between consecutive points, so the caller samples the path and
        /// the canvas fills the gaps: a drag sampled every few millimetres still paints a
        /// continuous line rather than a row of dots. A single point paints a round dab, which is
        /// what a tap with the brush means.
        /// </param>
        /// <param name="width">
        /// The stroke's full width in <em>metres</em>, for the same reason
        /// <see cref="FillCircle"/> takes metres: a width in <c>(u,v)</c> would be a different real
        /// thickness on every wall, and a different one along <c>u</c> than along <c>v</c>. Ends
        /// and corners are round, so a stroke has the width the user asked for whichever way it
        /// turns. Non-positive widths paint nothing.
        /// </param>
        /// <param name="color">The colour to paint.</param>
        /// <remarks>
        /// The whole polyline is one operation rather than a segment at a time because a stroke has
        /// to composite as a single shape: painted segment by segment, every overlap where one
        /// segment's soft edge fell on the next one's body would show as a seam down the line.
        /// <para>
        /// Clipped to the surface, like a circle: a stroke that runs off the wall is cut off by it.
        /// A null or empty path paints nothing and is not an error — it is a gesture that ended
        /// before it had anywhere to go.
        /// </para>
        /// </remarks>
        void StrokePolyline(IReadOnlyList<Vector2> points, float width, Color32 color);
    }
}
