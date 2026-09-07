using DecoreXR.Core;
using DecoreXR.Interaction;
using DecoreXR.Painting;
using UnityEngine;

namespace DecoreXR.App
{
    /// <summary>
    /// Turns a finished size gesture into a circle: when the user releases a drag on a wall with the
    /// circle tool chosen, a <see cref="CircleCommand"/> for that centre, radius and colour is pushed
    /// onto the history.
    /// </summary>
    /// <remarks>
    /// The other half of what <see cref="FillTool"/> does, and deliberately its twin: it pushes a
    /// command and stops there, touching no texture, canvas or renderer, because the history is the
    /// source of truth and <c>Painting</c> is what reacts to it (ADR 0003, architecture §4).
    /// <para>
    /// This is architecture §6 step 5 in one place — the tool from the palette, the colour from the
    /// palette, and the <c>(u,v)</c> and radius from the gesture, combined into one concrete command.
    /// It is also why the gesture in <c>Interaction</c> can know nothing about tools: the check of
    /// which tool is chosen belongs here, on the <c>App</c> side of that boundary.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CircleTool : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The press-and-drag that says where the circle goes and how big it is.")]
        [SerializeField] private SurfaceSizeGesture gesture;

        [Tooltip("The history a circle is recorded in. Painting redraws from it.")]
        [SerializeField] private PaintHistory history;

        [Tooltip("What the user has chosen to paint with. A circle is painted only while the circle " +
                 "tool is the chosen one, in the chosen colour (ADR 0009).")]
        [SerializeField] private PaletteState paletteState;

        private void Reset()
        {
            gesture = FindAnyObjectByType<SurfaceSizeGesture>();
            history = FindAnyObjectByType<PaintHistory>();
            paletteState = FindAnyObjectByType<PaletteState>();
        }

        private void OnEnable()
        {
            if (gesture == null || history == null || paletteState == null)
            {
                Debug.LogError(
                    $"[{nameof(CircleTool)}] Needs a {nameof(SurfaceSizeGesture)}, a " +
                    $"{nameof(PaintHistory)} and a {nameof(PaletteState)}; without them drawing a " +
                    "circle would do nothing. Assign them in the inspector.", this);
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

        private void OnGestureCompleted(SurfaceSizeGesture source)
        {
            // The gesture runs whatever tool is chosen, because it is the same press that chooses a
            // wall for a fill. Only the circle tool turns one into a shape.
            if (paletteState.ActiveTool != PaintTool.Circle)
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
                    $"[{nameof(CircleTool)}] The surface has no anchor id yet, so the circle is not " +
                    "painted. Draw it again in a moment.", this);
                return;
            }

            history.Push(new CircleCommand(
                surface.Id, source.CenterUv, source.Radius, paletteState.ActiveColor));
        }
    }
}
