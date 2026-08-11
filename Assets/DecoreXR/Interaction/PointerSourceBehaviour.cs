using System;
using UnityEngine;

namespace DecoreXR.Interaction
{
    /// <summary>
    /// Shared plumbing for <see cref="IPointerSource"/> components: samples the backing device once
    /// a frame, turns the raw pressed/not-pressed reading into pen-down and pen-up edges, and holds
    /// the invariant that a press always ends.
    /// </summary>
    /// <remarks>
    /// Subclasses supply only <see cref="TrySample"/> — where the ray comes from and what counts as
    /// a press. Everything the interface promises about edges and loss of tracking lives here, so
    /// the controller (M2-T1) and hand (M2-T2) sources cannot drift apart on it.
    /// </remarks>
    public abstract class PointerSourceBehaviour : MonoBehaviour, IPointerSource
    {
        private Ray ray;

        /// <inheritdoc />
        public abstract PointerKind Kind { get; }

        /// <inheritdoc />
        public bool IsActive { get; private set; }

        /// <inheritdoc />
        public Ray Ray => ray;

        /// <inheritdoc />
        public bool IsPressed { get; private set; }

        /// <inheritdoc />
        public event Action<IPointerSource> PenDown;

        /// <inheritdoc />
        public event Action<IPointerSource> PenUp;

        /// <summary>
        /// Reads the device for this frame. Return false when the pointer is not usable — untracked,
        /// unconfigured, out of view — and the base class will take it inactive and lift the pen.
        /// </summary>
        /// <param name="aim">The world-space aim ray. Ignored when the sample fails.</param>
        /// <param name="pressed">Whether the pen is down. Ignored when the sample fails.</param>
        protected abstract bool TrySample(out Ray aim, out bool pressed);

        protected virtual void Update() => Sample();

        /// <summary>
        /// Takes this frame's reading and publishes it. Exposed separately from <c>Update</c> so a
        /// source that has to read <em>other</em> sources can sample later in the frame instead.
        /// </summary>
        protected void Sample()
        {
            if (!TrySample(out var aim, out var pressed))
            {
                Deactivate();
                return;
            }

            IsActive = true;
            ray = aim;
            SetPressed(pressed);
        }

        protected virtual void OnDisable()
        {
            // Being switched off mid-stroke still has to close the stroke, or whatever is painting
            // would sit holding a pen that never lifts (architecture §8.5).
            Deactivate();
        }

        protected virtual void OnDestroy()
        {
            PenDown = null;
            PenUp = null;
        }

        private void Deactivate()
        {
            IsActive = false;

            // Order matters: the pen lifts while the aim still reads as it did, so a listener
            // finishing a stroke on PenUp sees where the pointer actually was.
            SetPressed(false);
        }

        private void SetPressed(bool pressed)
        {
            if (pressed == IsPressed)
            {
                return;
            }

            IsPressed = pressed;

            if (pressed)
            {
                PenDown?.Invoke(this);
            }
            else
            {
                PenUp?.Invoke(this);
            }
        }
    }
}
