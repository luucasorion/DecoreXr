using DecoreXR.Spatial;
using UnityEngine;

namespace DecoreXR.Interaction
{
    /// <summary>
    /// Where a pointer ray met a paintable surface: which surface, and where on it in the surface's
    /// own normalized <c>(u,v)</c> (architecture §6 step 4).
    /// </summary>
    /// <remarks>
    /// <c>(u,v)</c> rather than a world point is the whole point of this type. Paint commands are
    /// stored in surface space so they stay put when the anchor moves and can be re-rendered from
    /// the command list at any resolution (ADR 0003). <see cref="Point"/> comes along for what is
    /// inherently world-space work — placing a highlight or a cursor.
    /// </remarks>
    public readonly struct SurfaceHit
    {
        public SurfaceHit(IPaintableSurface surface, Vector2 uv, Vector3 point, float distance)
        {
            Surface = surface;
            Uv = uv;
            Point = point;
            Distance = distance;
        }

        /// <summary>The surface that was hit.</summary>
        public IPaintableSurface Surface { get; }

        /// <summary>Where on the surface, normalized 0→1 from its bottom-left corner.</summary>
        public Vector2 Uv { get; }

        /// <summary>The world-space point of the hit, on the surface plane.</summary>
        public Vector3 Point { get; }

        /// <summary>Distance from the ray origin to the hit, in metres.</summary>
        public float Distance { get; }
    }
}
