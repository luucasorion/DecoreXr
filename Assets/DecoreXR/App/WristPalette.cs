using UnityEngine;

namespace DecoreXR.App
{
    /// <summary>
    /// Puts the tool/colour palette on the user's off hand: a world-space uGUI panel that sits just
    /// above the wrist and appears when they glance at it (ADR 0009).
    /// </summary>
    /// <remarks>
    /// Placement is split between the two anchors on purpose. The panel's <em>position</em> comes
    /// from the wrist, which is what makes it always reachable and keeps it out of the room; its
    /// <em>orientation</em> faces the viewer rather than following the wrist's roll, because a panel
    /// that rotates with the forearm is unreadable at exactly the moment the user wants to read it.
    /// That, plus the smoothing below, is the answer to the jitter ADR 0009 names as its main risk.
    /// <para>
    /// It shows only while the user is looking at their wrist. Deciding that from where the head is
    /// aimed, rather than from which way the palm is turned, means this component needs no opinion
    /// about the hand rig's axis conventions and behaves the same whether the off hand is holding a
    /// controller or being tracked bare (ADR 0002).
    /// </para>
    /// <para>
    /// Presentation and placement only. It neither reads nor writes <see cref="PaletteState"/> — the
    /// buttons the panel carries do that — so what the user has chosen survives the panel being
    /// hidden, out of view, or destroyed.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WristPalette : MonoBehaviour
    {
        [Header("Anchors")]
        [Tooltip("The off hand's anchor on the camera rig — the hand that is NOT painting. The " +
                 "panel is positioned relative to this.")]
        [SerializeField] private Transform wristAnchor;

        [Tooltip("The viewer's head — normally the rig's centre-eye anchor. The panel turns to face " +
                 "this, and glancing towards the wrist from here is what shows the panel.")]
        [SerializeField] private Transform viewer;

        [Header("UI")]
        [Tooltip("Root of the palette panel — the world-space uGUI canvas holding the tool and " +
                 "colour buttons. Shown only while the user is looking at their wrist.")]
        [SerializeField] private GameObject panelRoot;

        [Header("Placement")]
        [Tooltip("Where the panel sits relative to the wrist anchor, in that anchor's own space and " +
                 "in metres. The default lifts it clear of the back of the hand so the panel does " +
                 "not intersect it.")]
        [SerializeField] private Vector3 wristOffset = new Vector3(0f, 0.08f, 0.02f);

        [Tooltip("How quickly the panel settles onto the wrist, in seconds. Larger is calmer and " +
                 "lags more; 0 follows the wrist exactly, jitter and all (ADR 0009).")]
        [Min(0f)]
        [SerializeField] private float followSmoothing = 0.08f;

        [Header("Visibility")]
        [Tooltip("How close to the centre of the user's view the wrist has to be for the panel to " +
                 "appear: the cosine of the angle between where they are looking and where the " +
                 "wrist is. 1 is dead centre, 0 is ninety degrees off. Around 0.8 is a deliberate " +
                 "glance rather than the wrist merely being somewhere in view.")]
        [Range(0f, 1f)]
        [SerializeField] private float glanceThreshold = 0.8f;

        [Tooltip("How far past the threshold the wrist has to come back before the panel closes, " +
                 "as a margin on the value above. Without it the panel flickers on and off while " +
                 "the user holds their gaze right on the edge.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float glanceHysteresis = 0.05f;

        private bool isShown;

        /// <summary>True while the panel is up.</summary>
        public bool IsShown => isShown;

        private void Reset()
        {
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                viewer = mainCamera.transform;
            }
        }

        private void OnEnable()
        {
            if (wristAnchor == null || viewer == null || panelRoot == null)
            {
                Debug.LogError(
                    $"[{nameof(WristPalette)}] Needs the off-hand wrist anchor, the viewer, and the " +
                    "panel root; without all three the palette would sit at the origin or never " +
                    "appear. Assign them in the inspector.", this);
                enabled = false;
                return;
            }

            // Whatever was on screen before is stale. Start hidden and let the first frame decide.
            isShown = false;
            panelRoot.SetActive(false);
        }

        private void OnDisable()
        {
            isShown = false;

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        /// <summary>
        /// LateUpdate, so the panel lands after whatever moved the rig's anchors this frame — the
        /// same reason the paint canvas follows its wall there.
        /// </summary>
        private void LateUpdate()
        {
            // The rig deactivates the anchor for a hand that is not being tracked. A panel floating
            // at the last place a hand was is worse than no panel (architecture §8.5).
            if (!wristAnchor.gameObject.activeInHierarchy)
            {
                Show(false);
                return;
            }

            var wasShown = isShown;
            Show(IsWristGlancedAt());

            if (!isShown)
            {
                return;
            }

            // Snap on the frame it appears. Smoothing in from wherever the panel was last shown would
            // have it fly across the room to the wrist.
            Place(snap: !wasShown);
        }

        /// <summary>
        /// Whether the user is looking at their wrist, with the threshold relaxed while the panel is
        /// already up so a gaze resting on the boundary does not strobe it.
        /// </summary>
        private bool IsWristGlancedAt()
        {
            var toWrist = wristAnchor.position - viewer.position;

            // Degenerate only if the wrist is inside the head; there is nothing sensible to measure.
            if (toWrist.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            var alignment = Vector3.Dot(viewer.forward, toWrist.normalized);
            var threshold = isShown ? glanceThreshold - glanceHysteresis : glanceThreshold;
            return alignment >= threshold;
        }

        private void Place(bool snap)
        {
            var target = wristAnchor.TransformPoint(wristOffset);

            var position = snap || followSmoothing <= 0f
                ? target
                // Framerate-independent exponential approach: the same visual smoothing at 72Hz and
                // at 90Hz, rather than a per-frame lerp that tightens as the frame rate rises.
                : Vector3.Lerp(
                    panelRoot.transform.position,
                    target,
                    1f - Mathf.Exp(-Time.deltaTime / followSmoothing));

            var awayFromViewer = position - viewer.position;

            // A uGUI canvas faces along its own +Z, so pointing +Z away from the viewer turns its
            // face towards them. World up as the reference keeps the panel's text level however the
            // head is tilted.
            var rotation = awayFromViewer.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(awayFromViewer, Vector3.up)
                : panelRoot.transform.rotation;

            panelRoot.transform.SetPositionAndRotation(position, rotation);
        }

        private void Show(bool show)
        {
            if (isShown == show)
            {
                return;
            }

            isShown = show;
            panelRoot.SetActive(show);
        }
    }
}
