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
    /// The colour comes from the palette rather than from a field here (ADR 0009). That is what
    /// makes a fill mean "the colour the user has chosen": this tool reads
    /// <see cref="PaletteState"/> at the moment of the press, so it needs no notification when the
    /// choice changes and cannot hold a colour that has gone stale.
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

        [Tooltip("What the user has chosen to paint with. A fill reads the active colour from here " +
                 "(ADR 0009).")]
        [SerializeField] private PaletteState paletteState;

        private void OnEnable()
        {
            if (selection == null || history == null || paletteState == null)
            {
                Debug.LogError(
                    $"[{nameof(FillTool)}] Needs a {nameof(SurfaceSelection)}, a " +
                    $"{nameof(PaintHistory)} and a {nameof(PaletteState)}; without them choosing a " +
                    "wall would do nothing, or would paint a colour the user never picked. Assign " +
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
            paletteState = FindAnyObjectByType<PaletteState>();
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

            history.Push(new FillCommand(surface.Id, paletteState.ActiveColor));
        }
    }
}
