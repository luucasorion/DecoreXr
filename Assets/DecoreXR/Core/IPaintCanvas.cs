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
    /// Each new tool adds the one operation it needs here. Only <see cref="FillAll"/> exists so far
    /// because M3 only paints whole walls; M5 and M6 bring shapes and strokes.
    /// </para>
    /// </remarks>
    public interface IPaintCanvas
    {
        /// <summary>Paints the entire surface one solid colour.</summary>
        void FillAll(Color32 color);
    }
}
