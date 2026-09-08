using DecoreXR.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DecoreXR.App
{
    /// <summary>
    /// Makes one palette button mean undo or redo: pressing it moves the user along the one global
    /// paint history, and it goes dead when there is nothing that way to go (ADR 0007, ADR 0009).
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="PaletteToolButton"/>, and deliberately shaped the same way —
    /// one component per button, told in the inspector which of the two it is, so the panel's
    /// contents stay a scene decision. What it writes to is different, though: a tool button sets a
    /// standing choice in <see cref="PaletteState"/>, while this one acts on
    /// <see cref="PaintHistory"/> at the moment it is pressed and changes nothing about what the
    /// next press on a wall will paint.
    /// <para>
    /// It reads its own enabled state back from the history's notifications rather than polling
    /// <c>CanUndo</c> every frame: the history only moves when something is painted, undone or
    /// redone, and all three say so. A greyed-out undo is also the honest answer to a user at the
    /// far end of their history — pressing it and having nothing happen would read as the app
    /// having missed the press (architecture §8.5).
    /// </para>
    /// <para>
    /// Nothing here re-renders anything. The press moves the history's mark and <c>Painting</c>
    /// redraws the one wall that changed (M7-T1), which is the same one-way flow a fill already
    /// follows (architecture §4).
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class PaletteHistoryButton : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The paint history this button moves through.")]
        [SerializeField] private PaintHistory history;

        [Tooltip("Whether this button undoes or redoes.")]
        [SerializeField] private HistoryAction action = HistoryAction.Undo;

        private Button button;

        /// <summary>Whether this button undoes or redoes.</summary>
        public HistoryAction Action => action;

        /// <summary>True while pressing this button would do something.</summary>
        public bool IsAvailable =>
            history != null && (action == HistoryAction.Undo ? history.CanUndo : history.CanRedo);

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void Reset()
        {
            history = FindAnyObjectByType<PaintHistory>();
        }

        private void OnEnable()
        {
            if (history == null)
            {
                Debug.LogError(
                    $"[{nameof(PaletteHistoryButton)}] No {nameof(PaintHistory)} assigned, so " +
                    $"pressing this button could not {action.ToString().ToLowerInvariant()} anything. " +
                    "Assign one in the inspector.", this);
                enabled = false;
                return;
            }

            button.onClick.AddListener(OnClicked);
            history.CommandPushed += OnHistoryMoved;
            history.CommandUndone += OnHistoryMoved;
            history.CommandRedone += OnHistoryMoved;

            // The panel is built hidden and shown later, so the first thing it does on appearing has
            // to be to catch up with wherever the history already is.
            Present();
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClicked);
            }

            if (history != null)
            {
                history.CommandPushed -= OnHistoryMoved;
                history.CommandUndone -= OnHistoryMoved;
                history.CommandRedone -= OnHistoryMoved;
            }
        }

        private void OnClicked()
        {
            if (action == HistoryAction.Undo)
            {
                history.Undo();
            }
            else
            {
                history.Redo();
            }

            // No Present() here. The move raises the history's event, which both this button and the
            // other one are listening to — and a move that would do nothing cannot be started, since
            // this button is not interactable when there is nowhere to go.
        }

        // Undoing is what makes a redo possible and painting is what makes it impossible again, so
        // every one of the three notifications can change what this button should look like.
        private void OnHistoryMoved(IPaintCommand command) => Present();

        private void Present()
        {
            button.interactable = IsAvailable;
        }
    }
}
