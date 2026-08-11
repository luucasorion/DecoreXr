using System;
using UnityEngine;

namespace DecoreXR.Interaction
{
    /// <summary>
    /// One thing the user can point and paint with: a world-space aim ray plus a pen that goes down
    /// and up. Controller and hand both implement it, and the painting pipeline consumes it without
    /// knowing which is active (ADR 0002, architecture §3).
    /// </summary>
    /// <remarks>
    /// <see cref="Ray"/> and <see cref="IsPressed"/> are a per-frame sample, so read them in the
    /// frame you act on rather than caching them. Only <see cref="IsActive"/> guarantees the ray
    /// means anything: an untracked controller or a hand that left view still has to answer, and it
    /// answers by going inactive rather than by handing back a stale aim.
    /// <para>
    /// A press always ends. If the source goes inactive mid-press — hand-tracking lost, controller
    /// put down — <see cref="PenUp"/> fires anyway, so a stroke can never be left open
    /// (architecture §8.5).
    /// </para>
    /// </remarks>
    public interface IPointerSource
    {
        /// <summary>Which physical input is behind this pointer.</summary>
        PointerKind Kind { get; }

        /// <summary>
        /// True while the pointer is tracked and usable. When false, <see cref="Ray"/> is stale and
        /// <see cref="IsPressed"/> is false.
        /// </summary>
        bool IsActive { get; }

        /// <summary>The world-space aim ray for this frame. Meaningful only while <see cref="IsActive"/>.</summary>
        Ray Ray { get; }

        /// <summary>True while the pen is down — trigger held, or fingers pinched.</summary>
        bool IsPressed { get; }

        /// <summary>Raised on the frame the pen goes down.</summary>
        event Action<IPointerSource> PenDown;

        /// <summary>
        /// Raised on the frame the pen comes up, including when the pointer was lost while pressed.
        /// </summary>
        event Action<IPointerSource> PenUp;
    }
}
