using UnityEngine;

namespace DecoreXR.Interaction
{
    /// <summary>
    /// One pointer standing in front of several: forwards whichever candidate source is currently
    /// usable, so the rest of the app holds a single <see cref="IPointerSource"/> and never asks
    /// whether a controller or a hand is driving it (ADR 0002).
    /// </summary>
    /// <remarks>
    /// The user picks by acting — put the controller down and raise a hand, and the ray follows.
    /// Two rules make that safe. A source that is mid-press keeps the pointer until it lets go, so a
    /// stroke is never handed to a different input halfway through. And a source already pressed
    /// wins over a merely active one, so picking up a controller and squeezing takes over
    /// immediately rather than waiting for the hand to drop out of view.
    /// <para>
    /// Sampling happens in <c>LateUpdate</c>, after the candidates have taken their own readings in
    /// <c>Update</c>, so what is forwarded is this frame's aim rather than last frame's.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ActivePointerSource : PointerSourceBehaviour
    {
        [Tooltip("Candidate pointers in priority order — the first usable one wins when none is " +
                 "already pressed. Put the controller first; it is the primary painting path.")]
        [SerializeField] private PointerSourceBehaviour[] candidates = new PointerSourceBehaviour[0];

        private PointerSourceBehaviour current;

        /// <summary>The pointer currently being forwarded, or null while none is usable.</summary>
        public IPointerSource Current => current;

        /// <inheritdoc />
        public override PointerKind Kind => current != null ? current.Kind : PointerKind.Controller;

        protected override void Update()
        {
            // Deliberately empty: see LateUpdate.
        }

        private void LateUpdate() => Sample();

        protected override void OnDisable()
        {
            base.OnDisable();
            current = null;
        }

        /// <inheritdoc />
        protected override bool TrySample(out Ray aim, out bool pressed)
        {
            aim = default;
            pressed = false;

            current = Choose();
            if (current == null)
            {
                return false;
            }

            aim = current.Ray;
            pressed = current.IsPressed;
            return true;
        }

        private PointerSourceBehaviour Choose()
        {
            // Never change horses mid-stroke.
            if (current != null && current.IsActive && current.IsPressed)
            {
                return current;
            }

            PointerSourceBehaviour firstActive = null;

            foreach (var candidate in candidates)
            {
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.IsActive)
                {
                    continue;
                }

                // A press is the user's clearest statement of intent, so it jumps the queue.
                if (candidate.IsPressed)
                {
                    return candidate;
                }

                firstActive ??= candidate;
            }

            return firstActive;
        }
    }
}
