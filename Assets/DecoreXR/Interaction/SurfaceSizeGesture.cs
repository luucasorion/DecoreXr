using System;
using DecoreXR.Spatial;
using UnityEngine;

namespace DecoreXR.Interaction
{
    /// <summary>
    /// The press-and-drag that gives a shape its size: pressing on a wall marks the centre, dragging
    /// away from it opens the radius, and releasing settles it.
    /// </summary>
    /// <remarks>
    /// The radius is a world distance in metres between two points on the wall plane, not a distance
    /// in <c>(u,v)</c>. That is what makes the shape the size the user drew: <c>(u,v)</c> is
    /// normalized per wall, so the same drag would mean a different real size on a wide wall than on
    /// a narrow one, and a different size along <c>u</c> than along <c>v</c>. Metres is also what
    /// <c>CircleCommand</c> takes, so nothing between here and the canvas has to convert (ADR 0003).
    /// <para>
    /// It reads the hit <see cref="SurfaceSelection"/> already computed this frame rather than
    /// raycasting again, which is what the execution order exists for, and it reads the press from
    /// the pointer directly so the release is resolved against this frame's aim.
    /// </para>
    /// <para>
    /// Knows nothing about tools. Which shape a finished gesture becomes — or whether it becomes one
    /// at all — is <c>App</c>'s to decide from the palette, because <c>Interaction</c> must not
    /// depend on <c>App</c> (architecture §4).
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(InteractionExecutionOrder.SurfaceGesture)]
    public sealed class SurfaceSizeGesture : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The pointer whose press opens and closes the gesture — normally the " +
                 "ActivePointerSource, so either controller or hand can draw a shape.")]
        [SerializeField] private PointerSourceBehaviour pointer;

        [Tooltip("Where this frame's aim already landed. The gesture uses its hover rather than " +
                 "raycasting the same ray a second time.")]
        [SerializeField] private SurfaceSelection selection;

        [Header("Gesture")]
        [Tooltip("The smallest radius that counts as a shape, in metres. A press and release with " +
                 "no drag is how a user cancels, and it is also what a mis-click looks like, so " +
                 "anything under this is discarded rather than painted as a speck.")]
        [Min(0f)]
        [SerializeField] private float minRadius = 0.02f;

        private bool wasPressed;

        /// <summary>True while a gesture is open — pressed, and not yet settled or abandoned.</summary>
        public bool InProgress { get; private set; }

        /// <summary>The surface the gesture is on. Meaningful only while <see cref="InProgress"/>.</summary>
        public IPaintableSurface Surface { get; private set; }

        /// <summary>Where the gesture was started, in the surface's normalized <c>(u,v)</c>.</summary>
        public Vector2 CenterUv { get; private set; }

        /// <summary>Where the gesture was started, in world space on the surface plane.</summary>
        public Vector3 CenterPoint { get; private set; }

        /// <summary>How far the drag has opened, in metres. Zero until the pointer moves.</summary>
        public float Radius { get; private set; }

        /// <summary>Raised when a press lands on a surface and a gesture opens.</summary>
        public event Action<SurfaceSizeGesture> Started;

        /// <summary>
        /// Raised while the gesture is open and <see cref="Radius"/> has changed — what a live
        /// preview would follow.
        /// </summary>
        public event Action<SurfaceSizeGesture> Changed;

        /// <summary>
        /// Raised when the gesture is released with a usable radius. The properties still hold the
        /// finished gesture when this fires, and are cleared afterwards.
        /// </summary>
        public event Action<SurfaceSizeGesture> Completed;

        /// <summary>
        /// Raised when the gesture ends with nothing to show for it: released too small, the pointer
        /// lost, or the wall gone (architecture §8.5). Anything showing a preview must listen to this
        /// as well as to <see cref="Completed"/>, or a cancelled gesture would leave one on screen.
        /// </summary>
        public event Action<SurfaceSizeGesture> Cancelled;

        private void Reset()
        {
            pointer = FindAnyObjectByType<ActivePointerSource>();
            selection = FindAnyObjectByType<SurfaceSelection>();
        }

        private void OnEnable()
        {
            if (pointer == null || selection == null)
            {
                Debug.LogError(
                    $"[{nameof(SurfaceSizeGesture)}] Needs both a pointer and a " +
                    $"{nameof(SurfaceSelection)}; without them a shape could never be sized. Assign " +
                    "them in the inspector.", this);
                enabled = false;
                return;
            }

            // A press that was down when this was disabled is not a gesture that resumes.
            wasPressed = false;
            Abandon();
        }

        private void OnDisable()
        {
            Abandon();
        }

        private void OnDestroy()
        {
            Started = null;
            Changed = null;
            Completed = null;
            Cancelled = null;
        }

        private void Update()
        {
            var pressed = pointer.IsActive && pointer.IsPressed;
            var isPressEdge = pressed && !wasPressed;
            wasPressed = pressed;

            if (!InProgress)
            {
                if (isPressEdge && selection.HasHover)
                {
                    Begin(selection.Hover);
                }

                return;
            }

            // The wall can go away mid-drag, and so can the hand. Either way there is no gesture
            // left to finish (architecture §8.5).
            if (!pointer.IsActive || Surface == null || !Surface.IsValid)
            {
                Cancel();
                return;
            }

            if (!pressed)
            {
                Settle();
                return;
            }

            Track();
        }

        private void Begin(SurfaceHit hit)
        {
            InProgress = true;
            Surface = hit.Surface;
            CenterUv = hit.Uv;
            CenterPoint = hit.Point;
            Radius = 0f;

            Started?.Invoke(this);
        }

        /// <summary>
        /// Opens the radius to this frame's aim.
        /// </summary>
        /// <remarks>
        /// Aiming off the wall holds the last radius rather than cancelling or collapsing it. The
        /// raycast only reports hits inside the rectangle, so overshooting the edge — which is
        /// exactly what a user does when drawing a circle as large as the wall allows — would
        /// otherwise throw the gesture away at the moment they were happiest with it.
        /// </remarks>
        private void Track()
        {
            if (!selection.HasHover || !ReferenceEquals(selection.Hover.Surface, Surface))
            {
                return;
            }

            // Both points lie on the surface plane, so their world distance is the in-plane distance
            // the user actually dragged.
            var radius = Vector3.Distance(CenterPoint, selection.Hover.Point);
            if (Mathf.Approximately(radius, Radius))
            {
                return;
            }

            Radius = radius;
            Changed?.Invoke(this);
        }

        private void Settle()
        {
            if (Radius < minRadius)
            {
                // A tap, or a drag too small to have meant anything. Not an error — it is how a user
                // backs out of a shape they have started.
                Cancel();
                return;
            }

            // Fired before the state is cleared, so a listener can read the finished gesture.
            Completed?.Invoke(this);
            Clear();
        }

        private void Cancel()
        {
            Cancelled?.Invoke(this);
            Clear();
        }

        /// <summary>
        /// Ends whatever is open because this component is going away or coming back, rather than
        /// because the user finished. An open gesture is still cancelled out loud: anything showing a
        /// preview has to be told, or it would leave one on screen for a gesture that no longer
        /// exists.
        /// </summary>
        private void Abandon()
        {
            if (InProgress)
            {
                Cancel();
                return;
            }

            Clear();
        }

        private void Clear()
        {
            InProgress = false;
            Surface = null;
            CenterUv = Vector2.zero;
            CenterPoint = Vector3.zero;
            Radius = 0f;
        }
    }
}
