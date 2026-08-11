using System;
using System.Threading.Tasks;
using Meta.XR.MRUtilityKit;
using UnityEngine;
using UnityEngine.Android;

namespace DecoreXR.Spatial
{
    /// <summary>
    /// Requests the Quest scene permission and loads the room's scene model through MRUK
    /// (ADR 0001). This is the single entry point to the scene system: MRUK types stay behind it
    /// (architecture §8.2), and the rest of the app only ever sees a <see cref="SceneLoadStatus"/>.
    /// </summary>
    /// <remarks>
    /// The MRUK component this drives must have <c>Load Scene On Startup</c> disabled — the load
    /// has to happen after the permission prompt is answered, not before it.
    /// A denied permission or an unscanned room is a normal outcome, not an error: it resolves to
    /// a terminal status that <c>App</c> turns into Space Setup guidance (M1-T3) or the manual
    /// plane fallback (M1-T4, ADR 0010), never a crash or a silent no-op (architecture §8.5).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SceneLoader : MonoBehaviour
    {
        [Tooltip("Load the scene model automatically on Start. Disable to drive the load manually.")]
        [SerializeField] private bool loadOnStart = true;

        [Tooltip("If the room has never been scanned, send the user straight into Space Setup " +
                 "rather than reporting NoSceneFound. Leave off to let App present the choice (ADR 0010).")]
        [SerializeField] private bool requestSpaceSetupIfMissing;

        private SceneLoadStatus status = SceneLoadStatus.NotStarted;
        private Task<SceneLoadStatus> inFlight;

        /// <summary>Raised whenever <see cref="Status"/> changes, including intermediate states.</summary>
        public event Action<SceneLoadStatus> StatusChanged;

        /// <summary>The most recent scene-load outcome.</summary>
        public SceneLoadStatus Status => status;

        /// <summary>True once a scene model with at least one room is loaded.</summary>
        public bool HasScene => status == SceneLoadStatus.Ready;

        private void Start()
        {
            if (loadOnStart)
            {
                _ = LoadAsync();
            }
        }

        private void OnDestroy()
        {
            // Drop subscribers so a reloaded scene never fires into dead objects (architecture §8.4).
            StatusChanged = null;
        }

        /// <summary>
        /// Requests scene permission if needed, then loads the scene model from the device.
        /// Concurrent calls share the in-flight load rather than starting a second one — MRUK
        /// allows only one discovery at a time.
        /// </summary>
        public Task<SceneLoadStatus> LoadAsync()
        {
            if (inFlight != null && !inFlight.IsCompleted)
            {
                return inFlight;
            }

            inFlight = LoadInternalAsync();
            return inFlight;
        }

        /// <summary>
        /// Sends the user into the system Space Setup flow and reloads the scene model when they
        /// return, so an unscanned room can be fixed without restarting the app (ADR 0010).
        /// Backing out of Space Setup is a normal outcome and leaves the status at
        /// <see cref="SceneLoadStatus.NoSceneFound"/> — the manual plane fallback stays available.
        /// </summary>
        public Task<SceneLoadStatus> RequestSpaceSetupAsync()
        {
            if (inFlight != null && !inFlight.IsCompleted)
            {
                return inFlight;
            }

            inFlight = RequestSpaceSetupInternalAsync();
            return inFlight;
        }

        private async Task<SceneLoadStatus> RequestSpaceSetupInternalAsync()
        {
            SetStatus(SceneLoadStatus.Loading);

            // Space Setup edits the scene model, so it needs the same permission the load does.
            var permitted = await RequestScenePermissionAsync();
            if (this == null)
            {
                return SceneLoadStatus.Failed;
            }

            if (!permitted)
            {
                return SetStatus(SceneLoadStatus.PermissionDenied);
            }

            bool captured;
            try
            {
                captured = await OVRScene.RequestSpaceSetup();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(SceneLoader)}] Space Setup could not be started: {exception}", this);
                return SetStatus(SceneLoadStatus.Failed);
            }

            if (this == null)
            {
                return SceneLoadStatus.Failed;
            }

            if (!captured)
            {
                // The user dismissed Space Setup without scanning. Still nothing to paint.
                return SetStatus(SceneLoadStatus.NoSceneFound);
            }

            // Reload in-place rather than through LoadAsync: this call already owns the in-flight slot.
            return await LoadInternalAsync();
        }

        private async Task<SceneLoadStatus> LoadInternalAsync()
        {
            SetStatus(SceneLoadStatus.Loading);

            var permitted = await RequestScenePermissionAsync();

            // The permission dialog and the load both outlive a scene change, so re-check that we
            // are still alive before touching anything (architecture §8.4).
            if (this == null)
            {
                return SceneLoadStatus.Failed;
            }

            if (!permitted)
            {
                return SetStatus(SceneLoadStatus.PermissionDenied);
            }

            var mruk = MRUK.Instance;
            if (mruk == null)
            {
                Debug.LogError(
                    $"[{nameof(SceneLoader)}] No MRUK instance in the scene; cannot load the scene model. " +
                    "Add the MR Utility Kit building block to the scene (ADR 0001, ADR 0013).", this);
                return SetStatus(SceneLoadStatus.Failed);
            }

            MRUK.LoadDeviceResult result;
            try
            {
                result = await mruk.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound: requestSpaceSetupIfMissing);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(SceneLoader)}] Loading the scene model threw: {exception}", this);
                return SetStatus(SceneLoadStatus.Failed);
            }

            return this == null ? SceneLoadStatus.Failed : SetStatus(Translate(result, mruk));
        }

        /// <summary>
        /// Maps MRUK's load result onto our vocabulary. A successful load with no rooms counts as
        /// <see cref="SceneLoadStatus.NoSceneFound"/> — there is nothing to paint either way.
        /// </summary>
        private SceneLoadStatus Translate(MRUK.LoadDeviceResult result, MRUK mruk)
        {
            switch (result)
            {
                case MRUK.LoadDeviceResult.Success:
                    return mruk.Rooms.Count > 0 ? SceneLoadStatus.Ready : SceneLoadStatus.NoSceneFound;

                case MRUK.LoadDeviceResult.NoScenePermission:
                    return SceneLoadStatus.PermissionDenied;

                case MRUK.LoadDeviceResult.NoRoomsFound:
                    return SceneLoadStatus.NoSceneFound;

                default:
                    Debug.LogWarning($"[{nameof(SceneLoader)}] Scene model load failed: {result}.", this);
                    return SceneLoadStatus.Failed;
            }
        }

        /// <summary>
        /// Ensures <c>com.oculus.permission.USE_SCENE</c> is granted, prompting once if it is not.
        /// Off-device (Editor / Simulator) there is no Android permission model, so this is a
        /// no-op and MRUK itself reports whether scene data is reachable (ADR 0011).
        /// </summary>
        private static Task<bool> RequestScenePermissionAsync()
        {
            const string scenePermission = OVRPermissionsRequester.ScenePermission;

            // The permission prompt only exists on the headset. In the Editor and the Simulator
            // no callback would ever fire, so treat it as granted and let MRUK be the judge.
            if (Application.platform != RuntimePlatform.Android ||
                Permission.HasUserAuthorizedPermission(scenePermission))
            {
                return Task.FromResult(true);
            }

            var completion = new TaskCompletionSource<bool>();
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => completion.TrySetResult(true);
            callbacks.PermissionDenied += _ => completion.TrySetResult(false);
            Permission.RequestUserPermission(scenePermission, callbacks);
            return completion.Task;
        }

        private SceneLoadStatus SetStatus(SceneLoadStatus next)
        {
            if (status == next)
            {
                return status;
            }

            status = next;
            StatusChanged?.Invoke(status);
            return status;
        }
    }
}
