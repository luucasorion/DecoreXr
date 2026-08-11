using DecoreXR.Spatial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DecoreXR.App
{
    /// <summary>
    /// Turns a failed scene load into something the user can act on: an explanation of why there
    /// are no walls, and a button that opens Space Setup and reloads (ADR 0010).
    /// This is the "communicated state" half of fail-to-error-state — without it a denied
    /// permission or an unscanned room would be a silent empty room (architecture §8.5).
    /// </summary>
    /// <remarks>
    /// Presentation only: it reads <see cref="SceneLoader.Status"/> and calls back through
    /// <c>Spatial</c>'s public surface, and holds no scene knowledge of its own (architecture §4).
    /// The panel is world-space uGUI in <c>App</c>, where UI lives (ADR 0009).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SceneGuidance : MonoBehaviour
    {
        [Header("Scene source")]
        [Tooltip("The loader whose status drives this panel.")]
        [SerializeField] private SceneLoader sceneLoader;

        [Header("UI")]
        [Tooltip("Root of the guidance panel. Shown only while the user has no usable scene.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("Label that explains the current problem.")]
        [SerializeField] private TMP_Text messageLabel;

        [Tooltip("Button that opens the system Space Setup flow and reloads afterwards.")]
        [SerializeField] private Button spaceSetupButton;

        private void Reset()
        {
            sceneLoader = FindAnyObjectByType<SceneLoader>();
        }

        private void OnEnable()
        {
            if (sceneLoader == null)
            {
                Debug.LogError(
                    $"[{nameof(SceneGuidance)}] No {nameof(SceneLoader)} assigned; the user would get " +
                    "no explanation for an empty room. Assign one in the inspector.", this);
                return;
            }

            sceneLoader.StatusChanged += OnStatusChanged;
            if (spaceSetupButton != null)
            {
                spaceSetupButton.onClick.AddListener(OnSpaceSetupClicked);
            }

            Present(sceneLoader.Status);
        }

        private void OnDisable()
        {
            if (sceneLoader != null)
            {
                sceneLoader.StatusChanged -= OnStatusChanged;
            }

            if (spaceSetupButton != null)
            {
                spaceSetupButton.onClick.RemoveListener(OnSpaceSetupClicked);
            }
        }

        private void OnStatusChanged(SceneLoadStatus status) => Present(status);

        private void Present(SceneLoadStatus status)
        {
            if (status == SceneLoadStatus.Loading)
            {
                // A Space Setup round-trip passes back through Loading. Leave whatever is on
                // screen alone rather than flickering the panel away and straight back, and lock
                // the button while the request is in flight.
                if (spaceSetupButton != null)
                {
                    spaceSetupButton.interactable = false;
                }

                return;
            }

            var needsHelp = status.NeedsFallback();

            if (panelRoot != null)
            {
                panelRoot.SetActive(needsHelp);
            }

            if (needsHelp && messageLabel != null)
            {
                messageLabel.text = MessageFor(status);
            }

            if (spaceSetupButton != null)
            {
                // Re-running Space Setup cannot fix a permission the user refused.
                spaceSetupButton.interactable = status != SceneLoadStatus.PermissionDenied;
            }
        }

        /// <summary>
        /// The user-facing explanation for each way of ending up without a scene. Each one names
        /// the cause and the way out, so the failure is actionable rather than just reported.
        /// </summary>
        private static string MessageFor(SceneLoadStatus status)
        {
            switch (status)
            {
                case SceneLoadStatus.PermissionDenied:
                    return "DecoreXR needs permission to use your room's layout before it can find " +
                           "walls to paint.\n\nGrant it in Settings > Privacy > Device Permissions, " +
                           "then restart the app.";

                case SceneLoadStatus.NoSceneFound:
                    return "No room scan found.\n\nRun Space Setup to map your walls, and DecoreXR " +
                           "will pick them up as soon as you're back.";

                default:
                    return "DecoreXR couldn't read your room's layout.\n\nTry running Space Setup " +
                           "again, or restart the app.";
            }
        }

        private void OnSpaceSetupClicked()
        {
            if (sceneLoader != null)
            {
                _ = sceneLoader.RequestSpaceSetupAsync();
            }
        }
    }
}
