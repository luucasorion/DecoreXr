using UnityEngine;

namespace DecoreXR.Interaction
{
    /// <summary>
    /// The hand half of dual input: aims along the tracked hand's pointer pose and treats an
    /// index-finger pinch as the pen (ADR 0002).
    /// </summary>
    /// <remarks>
    /// A pinch is a softer signal than a trigger, so two things guard it. Pinch strength gets
    /// separate press and release thresholds, because a single threshold flickers the pen on and off
    /// while the fingers hover at the boundary — which on a wall would stipple the paint. And a hand
    /// whose pose is only a guess takes the pointer inactive rather than painting from it: pausing
    /// is recoverable, a stroke across the wrong part of the wall is not (architecture §8.5).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HandPinchPointerSource : PointerSourceBehaviour
    {
        [Header("Hand")]
        [Tooltip("The hand that drives this pointer. Leave empty to find the matching OVRHand in " +
                 "the scene.")]
        [SerializeField] private OVRHand hand;

        [Tooltip("Which hand to look for when Hand is left empty.")]
        [SerializeField] private OVRHand.Hand handType = OVRHand.Hand.HandRight;

        [Header("Pinch")]
        [Tooltip("Pinch strength at which the pen goes down.")]
        [Range(0f, 1f)]
        [SerializeField] private float pinchOnThreshold = 0.8f;

        [Tooltip("Pinch strength the hand must fall below for the pen to come up. Keep it below the " +
                 "press threshold — the gap is what stops the pen chattering mid-stroke.")]
        [Range(0f, 1f)]
        [SerializeField] private float pinchOffThreshold = 0.5f;

        private bool searchedForHand;

        /// <inheritdoc />
        public override PointerKind Kind => PointerKind.Hand;

        private void OnValidate()
        {
            // An off threshold at or above the on threshold has no gap to debounce with, and the
            // pen would flutter exactly where a stroke is most delicate.
            pinchOffThreshold = Mathf.Min(pinchOffThreshold, pinchOnThreshold - 0.05f);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            searchedForHand = false;
        }

        /// <inheritdoc />
        protected override bool TrySample(out Ray aim, out bool pressed)
        {
            aim = default;
            pressed = false;

            var tracked = ResolveHand();
            if (tracked == null || !tracked.IsDataHighConfidence || !tracked.IsPointerPoseValid)
            {
                return false;
            }

            // Palm-up is the system menu gesture. The user is talking to the headset, not painting.
            if (tracked.IsSystemGestureInProgress)
            {
                return false;
            }

            var pose = tracked.PointerPose;
            if (pose == null)
            {
                return false;
            }

            aim = new Ray(pose.position, pose.forward);

            // IsPressed is still last frame's answer at this point, which is exactly the state the
            // hysteresis needs: hold a press until the fingers clearly part.
            var strength = tracked.GetFingerPinchStrength(OVRHand.HandFinger.Index);
            pressed = IsPressed ? strength > pinchOffThreshold : strength >= pinchOnThreshold;
            return true;
        }

        /// <summary>
        /// The hand, searched for once per enable when the inspector left it empty — see
        /// <c>ControllerPointerSource</c> for why the search is not repeated per frame.
        /// </summary>
        private OVRHand ResolveHand()
        {
            if (hand != null)
            {
                return hand;
            }

            if (searchedForHand)
            {
                return null;
            }

            searchedForHand = true;

            foreach (var candidate in FindObjectsByType<OVRHand>(FindObjectsSortMode.None))
            {
                if (candidate.GetHand() == (OVRPlugin.Hand)handType)
                {
                    hand = candidate;
                    break;
                }
            }

            if (hand == null)
            {
                Debug.LogError(
                    $"[{nameof(HandPinchPointerSource)}] No {handType} OVRHand in the scene, so this " +
                    "pointer will never aim. Add the Hand Tracking building block, or assign Hand " +
                    "in the inspector.", this);
            }

            return hand;
        }
    }
}
