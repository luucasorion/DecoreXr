using DecoreXR.Core;
using DecoreXR.Interaction;
using DecoreXR.Painting;
using UnityEngine;

namespace DecoreXR.App
{
    /// <summary>
    /// Turns a finished freehand path into paint: when the user releases a drag on a wall with the
    /// brush chosen, a <see cref="StrokeCommand"/> for that path, width and colour is pushed onto
    /// the history.
    /// </summary>
    /// <remarks>
    /// The third of the same shape as <see cref="FillTool"/> and <see cref="CircleTool"/>, and
    /// deliberately so: it pushes a command and stops there, touching no texture, canvas or
    /// renderer, because the history is the source of truth and <c>Painting</c> is what reacts to it
    /// (ADR 0003, architecture §4).
    /// <para>
    /// One command per completed stroke rather than one per sample, which is the granularity undo
    /// has to have (ADR 0007) — see <see cref="StrokeCommand"/>. It also means the renderer redraws
    /// the wall once when the user lifts off, not once a frame while they draw.
    /// </para>
    /// <para>
    /// This is architecture §6 step 5 again: the tool and colour from the palette, the path from the
    /// gesture, combined into one concrete command.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BrushTool : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The press-and-drag that traces where the stroke goes.")]
        [SerializeField] private SurfaceStrokeGesture gesture;

        [Tooltip("The history a stroke is recorded in. Painting redraws from it.")]
        [SerializeField] private PaintHistory history;

        [Tooltip("What the user has chosen to paint with. A stroke is painted only while the brush " +
                 "is the chosen tool, in the chosen colour (ADR 0009).")]
        [SerializeField] private PaletteState paletteState;

        [Header("Brush")]
        [Tooltip("How wide the stroke is painted, in metres. Config here rather than on the " +
                 "palette because the palette chooses tool and colour (ADR 0009); a width the user " +
                 "can change is not part of the MVP.")]
        [Min(0.001f)]
        [SerializeField] private float strokeWidth = 0.03f;

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
                    $"[{nameof(BrushTool)}] Needs a {nameof(SurfaceStrokeGesture)}, a " +
                    $"{nameof(PaintHistory)} and a {nameof(PaletteState)}; without them drawing a " +
                    "stroke would do nothing. Assign them in the inspector.", this);
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
            // wall for a fill. Only the brush turns one into a stroke.
            if (paletteState.ActiveTool != PaintTool.Brush)
            {
                return;
            }

            var surface = source.Surface;
            if (surface == null || !surface.IsValid)
            {
                return;
            }

            // A manual fallback plane has no id until its spatial anchor exists, which takes a moment
            // after it is placed (ADR 0010). Painting it now would give paint that cannot be
            // re-attached on load, so this is a wait-and-retry, not a failure (ADR 0006, §8.5).
            if (string.IsNullOrEmpty(surface.Id))
            {
                Debug.LogWarning(
                    $"[{nameof(BrushTool)}] The surface has no anchor id yet, so the stroke is not " +
                    "painted. Draw it again in a moment.", this);
                return;
            }

            // The command copies the path, so the gesture is free to reuse its buffer for the next
            // stroke (see StrokeCommand).
            history.Push(new StrokeCommand(
                surface.Id, source.Points, strokeWidth, paletteState.ActiveColor));
        }
    }
}
