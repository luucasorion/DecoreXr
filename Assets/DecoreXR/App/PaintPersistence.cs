using DecoreXR.Core;
using DecoreXR.Spatial;
using UnityEngine;

namespace DecoreXR.App
{
    /// <summary>
    /// Decides when the room is written down and when it is read back, so the user never has to
    /// (ADR 0006).
    /// </summary>
    /// <remarks>
    /// The triggers are the app's lifecycle rather than a button, and that is the whole design.
    /// ADR 0006 puts persistence in the MVP so that "painted walls survive app restarts" — a promise
    /// about closing the app, not about remembering to press save before doing so. On Quest the app is
    /// backgrounded and later reclaimed by the OS with no further warning, so a save that waited to be
    /// asked for would be a save that mostly never happened.
    /// <para>
    /// Three moments, for three different ways a session can end. <c>OnApplicationPause</c> is the one
    /// that matters most: taking the headset off or switching apps pauses this app, and everything
    /// after that point is the OS's choice. <c>OnApplicationQuit</c> covers a clean exit.
    /// And between them, a save a little while after the user stops painting, so a crash or a battery
    /// costs the last few seconds of work rather than the whole room.
    /// </para>
    /// <para>
    /// Loading is <see cref="PaintReattacher"/>'s, not this component's, because it has to wait for the
    /// room's walls and then decide which saved anchors are in it. This only offers a way to ask for
    /// it explicitly; left to itself, the reattacher loads as soon as the walls arrive.
    /// </para>
    /// <para>
    /// Deliberately not on the wrist palette. Persistence that needs a press is persistence that fails
    /// when the press does not happen, and the app currently has no input module feeding uGUI at all —
    /// so a save button would be a promise the build cannot keep. <see cref="Save"/> and
    /// <see cref="Load"/> are public for a palette control to call once there is one, which is a
    /// separate decision from this one.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PaintPersistence : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Where the room is written to and read from.")]
        [SerializeField] private PaintStore store;

        [Tooltip("The history that is saved. Also what is watched, so a save follows the painting.")]
        [SerializeField] private PaintHistory history;

        [Tooltip("What puts saved paint back on the walls. Optional: without it this component still " +
                 "saves, it just cannot be asked to load.")]
        [SerializeField] private PaintReattacher reattacher;

        [Header("Autosave")]
        [Tooltip("Save a while after the user stops painting, as well as when the app pauses or " +
                 "quits. Off leaves only the pause and quit saves.")]
        [SerializeField] private bool autosaveAfterPainting = true;

        [Tooltip("How long after the last paint action to save, in seconds. It is a delay rather " +
                 "than a save per action because a brush stroke is one command but a session is " +
                 "hundreds of them, and writing every wall's file on each would put file I/O in the " +
                 "middle of painting.")]
        [Min(0.5f)]
        [SerializeField] private float autosaveDelaySeconds = 5f;

        private bool isDirty;
        private float saveAtTime;

        /// <summary>True while there is painting that has happened since the last save.</summary>
        public bool HasUnsavedChanges => isDirty;

        /// <summary>How many times the room has been saved this session, successfully or not.</summary>
        public int SaveCount { get; private set; }

        /// <summary>Whether the last save went through. True before the first one.</summary>
        /// <remarks>
        /// Readable so that something can eventually tell the user — a failed save reports itself to
        /// the console, which nobody reads inside a headset. Saying it on the wrist is the same
        /// unowned problem as every other panel: the app has no input module feeding uGUI, so the
        /// banner that would carry it (<see cref="PaintHistoryBanner"/>'s shape, from M7-T2) has
        /// nothing to appear beside. Until then the honest mitigation is that a failure is retried
        /// rather than swallowed — see <see cref="Save"/>.
        /// </remarks>
        public bool LastSaveSucceeded { get; private set; } = true;

        private void Reset()
        {
            store = FindAnyObjectByType<PaintStore>();
            history = FindAnyObjectByType<PaintHistory>();
            reattacher = FindAnyObjectByType<PaintReattacher>();
        }

        private void OnEnable()
        {
            if (store == null || history == null)
            {
                Debug.LogError(
                    $"[{nameof(PaintPersistence)}] Needs a {nameof(PaintStore)} and a " +
                    $"{nameof(PaintHistory)}; without them the user's room would be lost every time " +
                    "they close the app (ADR 0006). Assign them in the inspector.", this);
                enabled = false;
                return;
            }

            // Every one of these means the walls now show something different from what is on disk.
            history.CommandPushed += OnHistoryChanged;
            history.CommandUndone += OnHistoryChanged;
            history.CommandRedone += OnHistoryChanged;

            // A restore is the one change that must not mark the room dirty: it just made the history
            // match the files, so saving straight back would be writing them out unchanged — and, if
            // the room was loaded before its walls arrived, writing out an emptier room than is saved.
            history.HistoryRestored += OnHistoryRestored;
        }

        private void OnDisable()
        {
            if (history != null)
            {
                history.CommandPushed -= OnHistoryChanged;
                history.CommandUndone -= OnHistoryChanged;
                history.CommandRedone -= OnHistoryChanged;
                history.HistoryRestored -= OnHistoryRestored;
            }

            // Whatever is unsaved is written down on the way out rather than dropped: this component
            // being switched off is not the user saying to forget their room (architecture §8.4).
            if (isDirty)
            {
                Save();
            }
        }

        private void Update()
        {
            if (!isDirty || !autosaveAfterPainting || Time.time < saveAtTime)
            {
                return;
            }

            Save();
        }

        /// <summary>
        /// Writes the room down now.
        /// </summary>
        /// <remarks>
        /// A save that fails leaves the room counting as unsaved and schedules another attempt, rather
        /// than clearing the mark and moving on. The failures worth designing for are transient — a
        /// full disk, a file the OS has briefly locked — and giving up on the first one would lose the
        /// user's paint for good over something that would have worked a moment later. What it must
        /// not do is retry every frame: one unwritable file would become a flood of identical errors
        /// with the user still painting into it. So the next attempt waits the same delay an autosave
        /// does, and the pause and quit saves get their own attempts regardless
        /// (architecture §8.5).
        /// </remarks>
        /// <returns>True if the whole room was written.</returns>
        public bool Save()
        {
            if (store == null || history == null)
            {
                return false;
            }

            SaveCount++;
            LastSaveSucceeded = store.Save(history);

            // Cleared only on success. store.Save has already said what went wrong.
            isDirty = !LastSaveSucceeded;
            if (isDirty)
            {
                saveAtTime = Time.time + autosaveDelaySeconds;
            }

            return LastSaveSucceeded;
        }

        /// <summary>
        /// Asks for the saved room to be put back on the walls, for an app that would rather say when
        /// than let the reattacher decide.
        /// </summary>
        /// <returns>True if the load happened and read everything on disk.</returns>
        public bool Load()
        {
            if (reattacher == null)
            {
                Debug.LogError(
                    $"[{nameof(PaintPersistence)}] No {nameof(PaintReattacher)} assigned, so there is " +
                    "nothing to match saved paint against this room's walls. Assign one in the " +
                    "inspector.", this);
                return false;
            }

            return reattacher.Load();
        }

        /// <summary>
        /// The app is going into the background — on Quest, the headset coming off or the user
        /// switching away. The last reliable moment to write anything.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused && isDirty)
            {
                Save();
            }
        }

        private void OnApplicationQuit()
        {
            if (isDirty)
            {
                Save();
            }
        }

        private void OnHistoryChanged(IPaintCommand command)
        {
            isDirty = true;
            saveAtTime = Time.time + autosaveDelaySeconds;
        }

        private void OnHistoryRestored()
        {
            isDirty = false;
        }
    }
}
