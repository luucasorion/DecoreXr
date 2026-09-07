using System;
using System.Collections.Generic;
using DecoreXR.Spatial;
using UnityEngine;

namespace DecoreXR.Interaction
{
    /// <summary>
    /// The press-drag-release that draws a freehand path: pressing on a wall starts a path, dragging
    /// traces it, and releasing finishes it.
    /// </summary>
    /// <remarks>
    /// The path is sampled by distance, not by frame. A point is kept only once the aim has moved a
    /// set number of millimetres across the wall, so a path costs what was drawn rather than how
    /// long the user took to draw it and how fast the app happened to be running — the same stroke
    /// is the same list of points at 72fps and at 30. The canvas interpolates between the samples,
    /// so the gaps this leaves are filled rather than visible (ADR 0003).
    /// <para>
    /// Spacing is measured in world metres between two points on the wall plane, for the reason
    /// <see cref="SurfaceSizeGesture"/> measures its radius that way: <c>(u,v)</c> is normalized per
    /// wall, so the same threshold would mean a different real distance on every wall. The points
    /// themselves are kept in <c>(u,v)</c>, which is what a command carries.
    /// </para>
    /// <para>
    /// Like the size gesture it reads the hit <see cref="SurfaceSelection"/> already computed this
    /// frame rather than raycasting again, and it knows nothing about tools: whether a finished path
    /// becomes a brush stroke, an erase, or nothing at all is <c>App</c>'s to decide from the
    /// palette, because <c>Interaction</c> must not depend on <c>App</c> (architecture §4).
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(InteractionExecutionOrder.SurfaceGesture)]
    public sealed class SurfaceStrokeGesture : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The pointer whose press opens and closes the stroke — normally the " +
                 "ActivePointerSource, so either controller or hand can draw.")]
        [SerializeField] private PointerSourceBehaviour pointer;

        [Tooltip("Where this frame's aim already landed. The gesture traces its hover rather than " +
                 "raycasting the same ray a second time.")]
        [SerializeField] private SurfaceSelection selection;

        [Header("Sampling")]
        [Tooltip("How far the aim must travel across the wall, in metres, before another point is " +
                 "kept. Small enough that a curve stays a curve, large enough that a held-still " +
                 "hand does not fill the path with duplicates of one place.")]
        [Min(0.001f)]
        [SerializeField] private float sampleSpacing = 0.005f;

        [Tooltip("The most points one stroke may hold. A stroke that reaches this stops sampling " +
                 "and is still drawn and still finishes; the cap only stops an unattended press " +
                 "growing without limit.")]
        [Min(2)]
        [SerializeField] private int maxSamples = 4096;

        private readonly List<Vector2> points = new List<Vector2>();

        private bool wasPressed;
        private bool capped;
        private Vector3 lastSamplePoint;

        /// <summary>True while a stroke is open — pressed, and not yet finished or abandoned.</summary>
        public bool InProgress { get; private set; }

        /// <summary>The surface the stroke is on. Meaningful only while <see cref="InProgress"/>.</summary>
        public IPaintableSurface Surface { get; private set; }

        /// <summary>
        /// The path so far, in the surface's normalized <c>(u,v)</c> and in drawn order.
        /// </summary>
        /// <remarks>
        /// A view onto the gesture's own buffer, not a copy: it is refilled by the next stroke and
        /// cleared when this one ends. Anything that has to outlive the gesture — a command, above
        /// all — copies it (see <c>StrokeCommand</c>), which is also why this does not allocate a
        /// list per stroke (architecture §7).
        /// </remarks>
        public IReadOnlyList<Vector2> Points => points;

        /// <summary>Raised when a press lands on a surface and a stroke opens.</summary>
        public event Action<SurfaceStrokeGesture> Started;

        /// <summary>
        /// Raised while the stroke is open and a point has just been added — what a live preview
        /// would follow.
        /// </summary>
        public event Action<SurfaceStrokeGesture> Changed;

        /// <summary>
        /// Raised when the stroke is released with a path to show for it. The properties still hold
        /// the finished stroke when this fires, and are cleared afterwards.
        /// </summary>
        public event Action<SurfaceStrokeGesture> Completed;

        /// <summary>
        /// Raised when the stroke ends with nothing to show for it: the pointer lost, or the wall
        /// gone (architecture §8.5). Anything showing a preview must listen to this as well as to
        /// <see cref="Completed"/>, or a cancelled stroke would leave one on screen.
        /// </summary>
        public event Action<SurfaceStrokeGesture> Cancelled;

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
                    $"[{nameof(SurfaceStrokeGesture)}] Needs both a pointer and a " +
                    $"{nameof(SurfaceSelection)}; without them a stroke could never be traced. " +
                    "Assign them in the inspector.", this);
                enabled = false;
                return;
            }

            // A press that was down when this was disabled is not a stroke that resumes.
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

            // The wall can go away mid-stroke, and so can the hand. Either way there is no stroke
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
            capped = false;

            points.Clear();
            points.Add(hit.Uv);
            lastSamplePoint = hit.Point;

            Started?.Invoke(this);
        }

        /// <summary>
        /// Adds this frame's aim to the path, if it has moved far enough to be a new point.
        /// </summary>
        /// <remarks>
        /// Aiming off the wall, or onto a different one, holds the path where it is rather than
        /// cancelling it or jumping across the room. A user tracing near an edge crosses it and
        /// comes back, and the stroke they meant is the one that stops at the edge — which is also
        /// what the canvas draws, since it clips to the wall.
        /// </remarks>
        private void Track()
        {
            if (capped || !selection.HasHover || !ReferenceEquals(selection.Hover.Surface, Surface))
            {
                return;
            }

            var hit = selection.Hover;

            // Both points lie on the surface plane, so their world distance is how far the aim
            // actually travelled across the wall.
            if (Vector3.Distance(lastSamplePoint, hit.Point) < sampleSpacing)
            {
                return;
            }

            points.Add(hit.Uv);
            lastSamplePoint = hit.Point;

            if (points.Count >= maxSamples)
            {
                // Said once, and the stroke goes on: the user still gets the path they drew up to
                // here, and still finishes it by releasing.
                capped = true;
                Debug.LogWarning(
                    $"[{nameof(SurfaceStrokeGesture)}] Stroke reached its {maxSamples}-point cap, so " +
                    "the rest of this drag is not sampled. Release and draw again to continue.", this);
            }

            Changed?.Invoke(this);
        }

        private void Settle()
        {
            if (points.Count == 0)
            {
                Cancel();
                return;
            }

            // A press and release without a drag is a single-point path, and that is deliberate: for
            // a brush it means a dab, which is a thing a user means to do. There is no minimum
            // length to clear, unlike a shape's radius (see SurfaceSizeGesture).
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
        /// because the user finished. An open stroke is still cancelled out loud: anything showing a
        /// preview has to be told, or it would leave one on screen for a stroke that no longer
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
            capped = false;
            lastSamplePoint = Vector3.zero;

            // Cleared, not replaced: the capacity this stroke reached is what the next one draws on
            // instead of allocating again.
            points.Clear();
        }
    }
}
