using System;
using System.Collections.Generic;

namespace DecoreXR.Spatial
{
    /// <summary>
    /// A source of <see cref="IPaintableSurface"/>s. MRUK walls are one source; the manual
    /// flat-plane fallback is another (ADR 0010). Consumers raycast and paint against whatever a
    /// provider yields, without caring where the surfaces came from.
    /// </summary>
    public interface IPaintableSurfaceProvider
    {
        /// <summary>
        /// The currently available surfaces. The list is replaced, not mutated, when the scene
        /// changes — callers may hold it for the duration of a frame.
        /// </summary>
        IReadOnlyList<IPaintableSurface> Surfaces { get; }

        /// <summary>
        /// Raised after <see cref="Surfaces"/> changes — a scene load, a re-scan, or a new manual
        /// plane. Anything caching a surface should re-read and drop invalid ones.
        /// </summary>
        event Action<IPaintableSurfaceProvider> SurfacesChanged;
    }
}
