using DecoreXR.Core;
using DecoreXR.Interaction;
using DecoreXR.Painting;
using UnityEngine;

namespace DecoreXR.App
{
    /// <summary>
    /// Turns a finished freehand path into an erase: when the user releases a drag on a wall with
    /// the eraser chosen, an <see cref="EraserCommand"/> for that path and width is pushed onto the
    /// history.
    /// </summary>
    /// <remarks>
    /// <see cref="BrushTool"/>'s twin, reading the same gesture — an erase is drawn exactly the way
    /// a stroke is, and the only difference the user should feel is what comes off instead of going
    /// on. Like every tool in <c>App</c> it pushes a command and stops there, touching no texture,
    /// canvas or renderer (ADR 0003, architecture §4).
    /// <para>
    /// It takes no colour from the palette, because an erase has none: what it leaves behind is
    /// unpainted wall (see <see cref="EraserCommand"/>). The colour swatches stay as they are while
    /// the eraser is chosen, so the user comes back to the colour they were painting with.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EraserTool : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The press-and-drag that traces what is erased — the same gesture the brush uses.")]
        [SerializeField] private SurfaceStrokeGesture gesture;

        [Tooltip("The history an erase is recorded in. Painting redraws from it, so the erase takes " +
                 "off whatever the commands before it painted.")]
        [SerializeField] private PaintHistory history;

        [Tooltip("What the user has chosen to paint with. Paint is erased only while the eraser is " +
                 "the chosen tool (ADR 0009). The chosen colour is not used — an erase has none.")]
        [SerializeField] private PaletteState paletteState;

        [Header("Eraser")]
        [Tooltip("How wide a band is erased, in metres. Wider than the brush by default, because " +
                 "an eraser is used to clear an area rather than to draw one.")]
        [Min(0.001f)]
        [SerializeField] private float eraserWidth = 0.06f;

        private void Reset()
        {
            gesture = FindAnyObjectByType<SurfaceStrokeGesture>();
            history = FindAnyObjectByType<PaintHistory>();
            paletteState = FindAnyObjectByType<PaletteState>();
        }

        private void OnEnable()
        {
            if (gesture == null || history == null || paletteState == null)
            {
                Debug.LogError(
                    $"[{nameof(EraserTool)}] Needs a {nameof(SurfaceStrokeGesture)}, a " +
                    $"{nameof(PaintHistory)} and a {nameof(PaletteState)}; without them erasing " +
                    "would do nothing. Assign them in the inspector.", this);
                enabled = false;
                return;
            }

            gesture.Completed += OnGestureCompleted;
        }

        private void OnDisable()
        {
            if (gesture != null)
            {
                gesture.Completed -= OnGestureCompleted;
            }
        }

        private void OnGestureCompleted(SurfaceStrokeGesture source)
        {
            // The gesture runs whatever tool is chosen, because it is the same press that chooses a
            // wall for a fill. Only the eraser turns one into an erase.
            if (paletteState.ActiveTool != PaintTool.Eraser)
            {
                return;
            }

            var surface = source.Surface;
            if (surface == null || !surface.IsValid)
            {
                return;
            }

            // A manual fallback plane has no id until its spatial anchor exists, which takes a moment
            // after it is placed (ADR 0010). An erase against no id could not be re-attached on load
            // any more than paint could, so this is a wait-and-retry (ADR 0006, §8.5).
            if (string.IsNullOrEmpty(surface.Id))
            {
                Debug.LogWarning(
                    $"[{nameof(EraserTool)}] The surface has no anchor id yet, so nothing is erased. " +
                    "Try again in a moment.", this);
                return;
            }

            // The command copies the path, so the gesture is free to reuse its buffer for the next
            // drag (see EraserCommand).
            history.Push(new EraserCommand(surface.Id, source.Points, eraserWidth));
        }
    }
}
