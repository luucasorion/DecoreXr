using DecoreXR.Core;
using DecoreXR.Interaction;
using TMPro;
using UnityEngine;

namespace DecoreXR.App
{
    /// <summary>
    /// Says what just went, and whether it went somewhere the user is not looking: a short line —
    /// "Undid Fill", "Redid Brush stroke on another wall" — shown for a moment after every undo or
    /// redo.
    /// </summary>
    /// <remarks>
    /// This is the answer to the risk ADR 0007 accepted when it chose one history for the whole room
    /// over one per wall: undo means "the last thing I did", and the last thing the user did may have
    /// been on a wall behind them. Without this, a press that changes nothing they can see is
    /// indistinguishable from a press the app missed. The wording is deliberately about the
    /// <em>action</em>, because that is what the user is holding in their head, and the wall only
    /// gets a mention when it is not the one they chose.
    /// <para>
    /// It asks the command what it was rather than looking at its type, so a tool added after the MVP
    /// captions itself with no change here (ADR 0003 — see <see cref="IPaintCommand.DisplayName"/>).
    /// Presentation only: it reads the history's notifications and writes a label, and never moves
    /// the history itself — that is <see cref="PaletteHistoryButton"/>'s job (architecture §4).
    /// </para>
    /// <para>
    /// Painting hides it rather than replacing it. A new command discards whatever was undone
    /// (see <c>PaintHistory.Push</c>), so a banner still offering to explain that undo would be
    /// describing a history that no longer exists.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PaintHistoryBanner : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("The history whose undo and redo this banner reports.")]
        [SerializeField] private PaintHistory history;

        [Tooltip("The user's chosen wall, used only to tell them when an undo landed on a different " +
                 "one (ADR 0007). Optional: without it the banner still names what was undone, it " +
                 "just cannot say where.")]
        [SerializeField] private SurfaceSelection selection;

        [Header("UI")]
        [Tooltip("Root of the banner. Shown for a moment after an undo or a redo, hidden otherwise.")]
        [SerializeField] private GameObject bannerRoot;

        [Tooltip("Label the message is written into.")]
        [SerializeField] private TMP_Text messageLabel;

        [Header("Timing")]
        [Tooltip("How long the banner stays up after an undo or redo, in seconds. Long enough to " +
                 "read a three-word line, short enough not to sit over the wall the user has gone " +
                 "back to painting.")]
        [Min(0.5f)]
        [SerializeField] private float visibleSeconds = 3f;

        private float hideAtTime;

        /// <summary>True while the banner is up.</summary>
        public bool IsShown { get; private set; }

        /// <summary>What the banner last said. Empty until the first undo or redo.</summary>
        public string Message { get; private set; } = string.Empty;

        private void Reset()
        {
            history = FindAnyObjectByType<PaintHistory>();
            selection = FindAnyObjectByType<SurfaceSelection>();
        }

        private void OnEnable()
        {
            if (history == null || bannerRoot == null || messageLabel == null)
            {
                Debug.LogError(
                    $"[{nameof(PaintHistoryBanner)}] Needs a {nameof(PaintHistory)}, a banner root " +
                    "and a label; without them an undo on a wall the user cannot see would be " +
                    "silent. Assign them in the inspector.", this);
                enabled = false;
                return;
            }

            history.CommandUndone += OnUndone;
            history.CommandRedone += OnRedone;
            history.CommandPushed += OnPushed;

            // Whatever was on screen before is stale — it described a history the user has since
            // moved on from.
            Message = string.Empty;
            Hide();
        }

        private void OnDisable()
        {
            if (history != null)
            {
                history.CommandUndone -= OnUndone;
                history.CommandRedone -= OnRedone;
                history.CommandPushed -= OnPushed;
            }

            Hide();
        }

        private void Update()
        {
            // Only while something is up; the banner costs nothing on the frames between undos.
            if (IsShown && Time.time >= hideAtTime)
            {
                Hide();
            }
        }

        private void OnUndone(IPaintCommand command) => Announce("Undid", command);

        private void OnRedone(IPaintCommand command) => Announce("Redid", command);

        private void OnPushed(IPaintCommand command) => Hide();

        private void Announce(string verb, IPaintCommand command)
        {
            if (command == null)
            {
                return;
            }

            Message = IsElsewhere(command.SurfaceId)
                ? $"{verb} {command.DisplayName} on another wall"
                : $"{verb} {command.DisplayName}";

            messageLabel.text = Message;
            hideAtTime = Time.time + visibleSeconds;
            Show();
        }

        /// <summary>
        /// Whether a command landed on a wall other than the one the user has chosen.
        /// </summary>
        /// <remarks>
        /// False whenever there is nothing to compare against — no selection wired, nothing selected,
        /// or a surface with no id yet. Saying "on another wall" needs to be true to be worth
        /// anything; a guess would send the user hunting round the room for paint that never moved.
        /// </remarks>
        private bool IsElsewhere(string surfaceId)
        {
            if (selection == null || selection.Selected == null || string.IsNullOrEmpty(surfaceId))
            {
                return false;
            }

            var selectedId = selection.Selected.Id;
            return !string.IsNullOrEmpty(selectedId) && selectedId != surfaceId;
        }

        private void Show()
        {
            IsShown = true;
            bannerRoot.SetActive(true);
        }

        private void Hide()
        {
            IsShown = false;

            if (bannerRoot != null)
            {
                bannerRoot.SetActive(false);
            }
        }
    }
}
