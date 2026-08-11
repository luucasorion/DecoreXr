using UnityEngine;

namespace DecoreXR.Spatial
{
    /// <summary>
    /// A flat, rectangular surface that can be painted. This is the seam between where a surface
    /// came from and what is done with it: an MRUK wall anchor and a manually placed plane both
    /// implement it, and <c>Painting</c>/<c>Interaction</c> consume it without ever knowing which
    /// they hold (ADR 0010, architecture §4).
    /// </summary>
    /// <remarks>
    /// The surface defines its own 2D coordinate frame:
    /// <see cref="Pose"/> sits at the centre of the paintable rectangle with +X along <c>u</c>,
    /// +Y along <c>v</c>, and +Z pointing out of the wall towards the room. Normalized
    /// <c>(u,v)</c> runs 0→1 bottom-left to top-right, which is also the texture space the
    /// per-wall canvas is drawn in (ADR 0003).
    /// The pose is read live rather than cached: alignment depends on following the spatial
    /// anchor, never a snapshot (architecture §7 "anchor stability").
    /// </remarks>
    public interface IPaintableSurface
    {
        /// <summary>
        /// Stable identity of the surface — the spatial anchor UUID for MRUK walls. Paint is
        /// persisted and re-attached under this key (ADR 0006).
        /// </summary>
        string Id { get; }

        /// <summary>
        /// False once the surface's anchor is gone (room re-scanned, object destroyed). Callers
        /// must check this rather than assume a held reference stays good (architecture §8.5).
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// World pose of the surface's centre: +X is <c>u</c>, +Y is <c>v</c>, +Z is the outward
        /// normal. Read live from the underlying anchor.
        /// </summary>
        Pose Pose { get; }

        /// <summary>Paintable extent in metres, as width (<c>u</c>) by height (<c>v</c>).</summary>
        Vector2 Size { get; }

        /// <summary>The outward-facing world normal — shorthand for <c>Pose.forward</c>.</summary>
        Vector3 Normal { get; }

        /// <summary>
        /// Projects a world point onto the surface plane and converts it to normalized
        /// <c>(u,v)</c>. Returns false when the projection falls outside the rectangle.
        /// </summary>
        bool TryGetUv(Vector3 worldPoint, out Vector2 uv);

        /// <summary>
        /// Converts normalized <c>(u,v)</c> back to a world point on the surface plane.
        /// Values outside 0→1 extrapolate rather than clamp.
        /// </summary>
        Vector3 GetWorldPoint(Vector2 uv);
    }
}
