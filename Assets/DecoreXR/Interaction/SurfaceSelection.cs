using System;
using DecoreXR.Spatial;
using UnityEngine;

namespace DecoreXR.Interaction
{
    /// <summary>
    /// The end of the interaction chain: tracks what the pointer is aimed at from frame to frame, and
    /// commits that to a selection when the user presses (architecture §6 steps 3–5).
    /// </summary>
    /// <remarks>
    /// Hover and selection are deliberately separate. Hover is where the pointer happens to be
    /// pointing right now and changes constantly; the selection is the wall the user chose and has to
    /// survive them looking away from it, because the next thing they do is reach for a colour.
    /// <para>
    /// The press is read as an edge on <see cref="IPointerSource.IsPressed"/> here rather than through
    /// the source's pen-down event, so the press is resolved against the hit computed in the same
    /// frame. Acting on the event would select whatever was under the pointer one frame earlier.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(InteractionExecutionOrder.Selection)]
    public sealed class SurfaceSelection : MonoBehaviour
    {
        [Tooltip("The pointer that aims and presses — normally the ActivePointerSource, so either " +
                 "controller or hand can drive selection.")]
        [SerializeField] private PointerSourceBehaviour pointer;

        [Tooltip("The raycaster that turns the pointer's ray into a surface hit.")]
        [SerializeField] private SurfaceRaycaster raycaster;

        private bool wasPressed;

        /// <summary>True while the pointer is aimed at a paintable surface.</summary>
        public bool HasHover { get; private set; }

        /// <summary>Where the pointer is aimed. Meaningful only while <see cref="HasHover"/>.</summary>
        public SurfaceHit Hover { get; private set; }

        /// <summary>The surface the user chose, or null if none is chosen.</summary>
        public IPaintableSurface Selected { get; private set; }

        /// <summary>Raised whenever the hovered surface changes, including to and from nothing.</summary>
        public event Action<SurfaceSelection> HoverChanged;

        /// <summary>Raised whenever <see cref="Selected"/> changes.</summary>
        public event Action<SurfaceSelection> SelectionChanged;

        private void OnEnable()
        {
            if (pointer == null || raycaster == null)
            {
                Debug.LogError(
                    $"[{nameof(SurfaceSelection)}] Needs both a pointer and a {nameof(SurfaceRaycaster)}; " +
                    "without them nothing can be aimed at or chosen. Assign them in the inspector.", this);
                enabled = false;
                return;
            }

            // Start from a clean slate: whatever was hovered before being disabled is long stale.
            wasPressed = false;
            SetHover(false, default);
        }

        private void OnDisable()
        {
            SetHover(false, default);
        }

        private void OnDestroy()
        {
            HoverChanged = null;
            SelectionChanged = null;
        }

        private void Update()
        {
            DropSelectionIfGone();
            RefreshHover();
            ApplyPress();
        }

        /// <summary>Clears the selection outright, e.g. when a tool is done with it.</summary>
        public void ClearSelection() => Select(null);

        /// <summary>
        /// A selected wall can vanish under the user — the room gets re-scanned, or a manual plane is
        /// removed. Letting go of it is the clean fail; holding a dead surface would have the next
        /// paint command land nowhere (architecture §8.5).
        /// </summary>
        private void DropSelectionIfGone()
        {
            if (Selected != null && !Selected.IsValid)
            {
                Select(null);
            }
        }

        private void RefreshHover()
        {
            if (!pointer.IsActive)
            {
                SetHover(false, default);
                return;
            }

            if (raycaster.TryRaycast(pointer.Ray, out var hit))
            {
                SetHover(true, hit);
            }
            else
            {
                SetHover(false, default);
            }
        }

        private void ApplyPress()
        {
            var pressed = pointer.IsPressed;
            var isPressEdge = pressed && !wasPressed;
            wasPressed = pressed;

            // Pressing while aimed at nothing is left alone rather than treated as "deselect": the
            // user's aim drifting off a wall mid-gesture should not silently throw away their choice.
            if (isPressEdge && HasHover)
            {
                Select(Hover.Surface);
            }
        }

        private void SetHover(bool has, SurfaceHit hit)
        {
            var previous = HasHover ? Hover.Surface : null;
            var next = has ? hit.Surface : null;

            HasHover = has;
            Hover = hit;

            // Only a change of surface is announced — entering one, leaving one, crossing to
            // another. The aim moves every frame, so an event per frame would say nothing; anything
            // that has to follow the aim itself (a cursor, an outline) reads Hover each frame.
            if (!ReferenceEquals(previous, next))
            {
                HoverChanged?.Invoke(this);
            }
        }

        private void Select(IPaintableSurface surface)
        {
            if (ReferenceEquals(surface, Selected))
            {
                return;
            }

            Selected = surface;
            SelectionChanged?.Invoke(this);
        }
    }
}
