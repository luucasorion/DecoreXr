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
    /// paint and <see cref="FillCircle"/> is M5's shape; M6 brings strokes.
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
    }
}
