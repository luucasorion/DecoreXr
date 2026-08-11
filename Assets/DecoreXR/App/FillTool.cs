using DecoreXR.Core;
using DecoreXR.Interaction;
using DecoreXR.Painting;
using UnityEngine;

namespace DecoreXR.App
{
    /// <summary>
    /// Turns choosing a wall into painting it: when the user selects a surface, a
    /// <see cref="FillCommand"/> for the current colour is pushed onto the history. This is the last
    /// link in the MVP's first vertical slice — passthrough, scene, aim, select, paint.
    /// </summary>
    /// <remarks>
    /// This is the whole of the composition: the tool pushes a command and stops there. It does not
    /// touch a texture, a canvas or a renderer, because the history is the source of truth and
    /// <c>Painting</c> is what reacts to it (ADR 0003, architecture §4). That is also why undo will
    /// work in M7 without this file changing.
    /// <para>
    /// One serialized colour, because M3 is "solid-color fill" and nothing more. Choosing a tool and
    /// choosing a colour arrive with the wrist palette in M5 (ADR 0009); this component's job then
    /// becomes reading that state instead of its own field.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FillTool : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The selection that says which wall the user chose.")]
        [SerializeField] private SurfaceSelection selection;

        [Tooltip("The history a fill is recorded in. Painting redraws from it.")]
        [SerializeField] private PaintHistory history;

        [Header("Paint")]
        [Tooltip("The colour a fill paints. A single colour is all M3 has; the palette brings more " +
                 "in M5 (ADR 0009).")]
        [SerializeField] private Color fillColor = new Color(0.35f, 0.52f, 0.78f, 1f);

        /// <summary>The colour fills are painted in.</summary>
        public Color FillColor
        {
            get => fillColor;
            set => fillColor = value;
        }

        private void OnEnable()
        {
            if (selection == null || history == null)
            {
                Debug.LogError(
                    $"[{nameof(FillTool)}] Needs both a {nameof(SurfaceSelection)} and a " +
                    $"{nameof(PaintHistory)}; without them choosing a wall would do nothing. Assign " +
                    "them in the inspector.", this);
                enabled = false;
                return;
            }

            selection.SelectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            if (selection != null)
            {
                selection.SelectionChanged -= OnSelectionChanged;
            }
        }

        private void Reset()
        {
            selection = FindAnyObjectByType<SurfaceSelection>();
            history = FindAnyObjectByType<PaintHistory>();
        }

        private void OnSelectionChanged(SurfaceSelection source)
        {
            var surface = source.Selected;

            // Deselecting is not a paint action. Selection also clears itself when the chosen wall
            // goes away, and that must not paint anything either.
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
                    $"[{nameof(FillTool)}] The chosen surface has no anchor id yet, so it is not " +
                    "painted. Select it again in a moment.", this);
                return;
            }

            history.Push(new FillCommand(surface.Id, fillColor));
        }
    }
}
