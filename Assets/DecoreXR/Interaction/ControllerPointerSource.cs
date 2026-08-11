using UnityEngine;

namespace DecoreXR.Interaction
{
    /// <summary>
    /// The controller half of dual input: aims along the tracked controller and treats the index
    /// trigger as the pen (ADR 0002). This is the primary painting path for the MVP — the trigger
    /// gives an unambiguous pen down and up that a pinch cannot match.
    /// </summary>
    /// <remarks>
    /// The ray starts at one of the rig's controller anchors, which is already the pointing pose
    /// Meta's own rays use, so aiming matches what the user sees. Meta input APIs stay inside the
    /// concrete pointer sources, the same way MRUK stays inside a single adapter in <c>Spatial</c>
    /// (architecture §8.2).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ControllerPointerSource : PointerSourceBehaviour
    {
        [Header("Controller")]
        [Tooltip("Which controller drives this pointer. One source per hand; the right hand is the " +
                 "usual painting hand.")]
        [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.RTouch;

        [Tooltip("The button that means pen down.")]
        [SerializeField] private OVRInput.Button penButton = OVRInput.Button.PrimaryIndexTrigger;

        [Header("Ray")]
        [Tooltip("Transform the aim ray starts from, pointing along its +Z. Leave empty to use the " +
                 "matching controller anchor on the OVRCameraRig in the scene.")]
        [SerializeField] private Transform rayOrigin;

        private bool searchedForRigAnchor;

        /// <inheritdoc />
        public override PointerKind Kind => PointerKind.Controller;

        private void Reset()
        {
            rayOrigin = FindRigAnchor();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // Re-enabling is the one moment worth looking for the rig again — a scene reload may
            // have brought a different one.
            searchedForRigAnchor = false;
        }

        /// <inheritdoc />
        protected override bool TrySample(out Ray aim, out bool pressed)
        {
            aim = default;
            pressed = false;

            var origin = ResolveRayOrigin();
            if (origin == null)
            {
                return false;
            }

            // Connected is not the same as tracked: a controller resting on a desk is connected but
            // has nothing to aim with, and pointing from its last known pose would be a lie.
            if (!OVRInput.IsControllerConnected(controller) ||
                !OVRInput.GetControllerOrientationValid(controller))
            {
                return false;
            }

            aim = new Ray(origin.position, origin.forward);
            pressed = OVRInput.Get(penButton, controller);
            return true;
        }

        /// <summary>
        /// The ray origin, resolving it from the rig on first use if the inspector left it empty.
        /// Resolution is deferred rather than done in <c>Awake</c> because the rig fills its anchor
        /// properties in its own <c>Awake</c>, and the order between the two is not ours to decide.
        /// </summary>
        /// <remarks>
        /// The rig is searched for at most once per enable. This is called every frame, and both a
        /// scene-wide search and a log line are too expensive to repeat at frame rate on a mobile
        /// target (architecture §7).
        /// </remarks>
        private Transform ResolveRayOrigin()
        {
            if (rayOrigin != null)
            {
                return rayOrigin;
            }

            if (searchedForRigAnchor)
            {
                return null;
            }

            searchedForRigAnchor = true;
            rayOrigin = FindRigAnchor();

            if (rayOrigin == null)
            {
                Debug.LogError(
                    $"[{nameof(ControllerPointerSource)}] No ray origin for {controller} and no " +
                    "OVRCameraRig anchor to fall back on, so this pointer will never aim. Assign " +
                    "Ray Origin in the inspector.", this);
            }

            return rayOrigin;
        }

        private Transform FindRigAnchor()
        {
            var rig = FindAnyObjectByType<OVRCameraRig>();
            if (rig == null)
            {
                return null;
            }

            return controller == OVRInput.Controller.LTouch
                ? rig.leftControllerAnchor
                : rig.rightControllerAnchor;
        }
    }
}
