using System;
using System.Collections.Generic;
using UnityEngine;

namespace DecoreXR.Core
{
    /// <summary>
    /// The one list of everything the user has painted, in the order they did it, across every wall
    /// in the room (ADR 0007). Pushing to it is how a paint action becomes real: the canvases are
    /// rendered from this, never the other way round (ADR 0003). Undoing takes the last thing back
    /// off it, and redoing puts it back.
    /// </summary>
    /// <remarks>
    /// One global history rather than one per wall, because "undo" means "undo the last thing I did"
    /// regardless of which wall that was (ADR 0007) — and because the same history is meant to cover
    /// furniture and the rest of the decoration later, not just paint.
    /// <para>
    /// A <c>MonoBehaviour</c> so the history is a real object in the scene that the composition root
    /// hands to whoever needs it, wired in the inspector like the rest of the app. That keeps it out
    /// of being a static singleton reachable from anywhere, which architecture §4 rules out.
    /// </para>
    /// <para>
    /// Undo does not delete. The list holds done and undone commands together and remembers how far
    /// along it the user currently is, so undo and redo move that mark rather than edit the list.
    /// Two things fall out of that shape: an undone command comes back exactly as it was rather than
    /// being reconstructed, and the render path does not change at all — a canvas is still the replay
    /// of its surface's commands (ADR 0003), just of however many of them are currently done.
    /// </para>
    /// <para>
    /// Writing this list to disk under each anchor UUID is M8's job (ADR 0006); it reads the same
    /// append-ordered list, so that does not need this to change shape either.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PaintHistory : MonoBehaviour
    {
        // Done commands first, then any that have been undone. Everything before doneCount is paint
        // the user currently has; everything from doneCount on is paint they took back and could ask
        // for again.
        private readonly List<IPaintCommand> commands = new List<IPaintCommand>();
        private int doneCount;

        /// <summary>How many commands are currently done — what the walls show.</summary>
        public int Count => doneCount;

        /// <summary>How many undone commands are waiting to be redone.</summary>
        public int UndoneCount => commands.Count - doneCount;

        /// <summary>True while there is something to take back.</summary>
        public bool CanUndo => doneCount > 0;

        /// <summary>True while there is something to put back.</summary>
        public bool CanRedo => doneCount < commands.Count;

        /// <summary>
        /// Raised after a command is accepted, carrying it so a listener can re-render just the one
        /// surface it names instead of every canvas in the room (architecture §4).
        /// </summary>
        public event Action<IPaintCommand> CommandPushed;

        /// <summary>
        /// Raised after a command is taken back, carrying the command that was undone — which names
        /// the one surface to re-render, and what to tell the user has just gone (ADR 0007).
        /// </summary>
        public event Action<IPaintCommand> CommandUndone;

        /// <summary>Raised after an undone command is put back, carrying that command.</summary>
        public event Action<IPaintCommand> CommandRedone;

        /// <summary>
        /// Raised after <see cref="Restore"/> has replaced the whole history — a room read back off
        /// disk (ADR 0006).
        /// </summary>
        /// <remarks>
        /// Carries nothing, unlike the three above. They each name the one surface that changed, which
        /// is what lets a listener re-render only that wall; a restore changes every wall at once and
        /// has no single answer, so a listener asks <see cref="CollectSurfaceIds"/> which walls the
        /// history now covers rather than being handed a list it could hold on to.
        /// </remarks>
        public event Action HistoryRestored;

        /// <summary>
        /// Records a command as done. Rejects a null command, or one that names no surface, with an
        /// error rather than storing it: such a command can never be rendered or re-attached on load,
        /// so keeping it would turn a wiring mistake into paint that silently never appears
        /// (architecture §8.5).
        /// </summary>
        /// <remarks>
        /// Painting after undoing discards what was undone. The user chose a different next action,
        /// and keeping the old branch reachable would mean a redo that brings paint back on top of
        /// unrelated later paint — the history would no longer be the order they did things in,
        /// which is the one thing ADR 0007 asks of it.
        /// </remarks>
        /// <returns>True if the command was recorded.</returns>
        public bool Push(IPaintCommand command)
        {
            if (command == null)
            {
                Debug.LogError($"[{nameof(PaintHistory)}] Refused a null command.", this);
                return false;
            }

            if (string.IsNullOrEmpty(command.SurfaceId))
            {
                Debug.LogError(
                    $"[{nameof(PaintHistory)}] Refused a {command.GetType().Name} with no " +
                    $"{nameof(IPaintCommand.SurfaceId)}: there is no surface to render it on or to " +
                    "re-attach it to on load.", this);
                return false;
            }

            // Dropping the undone tail needs no re-render: each of those commands was already taken
            // off its wall at the moment it was undone.
            if (CanRedo)
            {
                commands.RemoveRange(doneCount, commands.Count - doneCount);
            }

            commands.Add(command);
            doneCount = commands.Count;
            CommandPushed?.Invoke(command);
            return true;
        }

        /// <summary>
        /// Takes back the last thing the user did, wherever in the room they did it (ADR 0007).
        /// </summary>
        /// <returns>
        /// The command that was undone, or null if there was nothing to undo — not an error, just
        /// the far end of the history.
        /// </returns>
        public IPaintCommand Undo()
        {
            if (!CanUndo)
            {
                return null;
            }

            doneCount--;
            var undone = commands[doneCount];
            CommandUndone?.Invoke(undone);
            return undone;
        }

        /// <summary>
        /// Puts back the most recently undone command.
        /// </summary>
        /// <returns>The command that was redone, or null if there was nothing to redo.</returns>
        public IPaintCommand Redo()
        {
            if (!CanRedo)
            {
                return null;
            }

            var redone = commands[doneCount];
            doneCount++;
            CommandRedone?.Invoke(redone);
            return redone;
        }

        /// <summary>
        /// Appends every <em>done</em> command targeting <paramref name="surfaceId"/> to
        /// <paramref name="results"/>, oldest first, which is the order a canvas has to draw them in
        /// for later paint to land on top of earlier paint.
        /// </summary>
        /// <remarks>
        /// Undone commands are skipped, which is the whole of how undo reaches the picture:
        /// re-rendering a surface is the same call it always was and needs no notion of undo of its
        /// own (ADR 0007).
        /// <para>
        /// Fills a caller-owned list instead of returning one so that re-rendering a wall reuses a
        /// buffer rather than allocating per render (architecture §7). The list is cleared first, so
        /// the caller gets that wall's commands and nothing else.
        /// </para>
        /// </remarks>
        public void CollectFor(string surfaceId, List<IPaintCommand> results)
        {
            if (results == null)
            {
                Debug.LogError($"[{nameof(PaintHistory)}] {nameof(CollectFor)} needs a list to fill.", this);
                return;
            }

            results.Clear();

            if (string.IsNullOrEmpty(surfaceId))
            {
                return;
            }

            // Indexed, not foreach: a foreach over List<T> through this call site boxes nothing today
            // but the explicit loop keeps it obvious that re-rendering must not allocate.
            for (var i = 0; i < doneCount; i++)
            {
                if (string.Equals(commands[i].SurfaceId, surfaceId, StringComparison.Ordinal))
                {
                    results.Add(commands[i]);
                }
            }
        }

        /// <summary>
        /// Appends every <em>done</em> command to <paramref name="results"/>, oldest first — the
        /// whole room, in the order the user painted it.
        /// </summary>
        /// <remarks>
        /// What saving needs (ADR 0006), and the reason it is the done ones and in this order. Done,
        /// because the file is meant to be what the walls look like, and ADR 0007 promises undo within
        /// a session rather than across a restart — writing the undone tail down would leave a redo
        /// button live on launch, offering to put back paint from a session the user has left. In
        /// order, because the writer keeps each command's place in this list so that per-wall files
        /// (ADR 0006) merge back into one global history (ADR 0007) rather than into a room grouped
        /// by wall.
        /// <para>
        /// The counterpart to <see cref="CollectFor"/>, and the same shape: a caller-owned list,
        /// cleared first, so saving reuses a buffer instead of allocating one per save.
        /// </para>
        /// </remarks>
        public void CollectDone(List<IPaintCommand> results)
        {
            if (results == null)
            {
                Debug.LogError($"[{nameof(PaintHistory)}] {nameof(CollectDone)} needs a list to fill.", this);
                return;
            }

            results.Clear();

            for (var i = 0; i < doneCount; i++)
            {
                results.Add(commands[i]);
            }
        }

        /// <summary>
        /// Appends the distinct surfaces the <em>done</em> commands paint, in the order each was
        /// first painted — the walls that currently have something on them.
        /// </summary>
        /// <remarks>
        /// What a listener asks after <see cref="HistoryRestored"/>: which walls to draw. Distinct,
        /// because re-rendering a wall replays all of its commands at once, so a wall with forty
        /// commands on it is one redraw and not forty.
        /// <para>
        /// A caller-owned list, cleared first, like <see cref="CollectFor"/> and
        /// <see cref="CollectDone"/>. Linear in the history and quadratic in the number of walls,
        /// which is the right way round: a room has a handful of walls and a session has thousands of
        /// commands, and a set would allocate on every call to avoid a scan of six strings.
        /// </para>
        /// </remarks>
        public void CollectSurfaceIds(List<string> results)
        {
            if (results == null)
            {
                Debug.LogError($"[{nameof(PaintHistory)}] {nameof(CollectSurfaceIds)} needs a list to fill.", this);
                return;
            }

            results.Clear();

            for (var i = 0; i < doneCount; i++)
            {
                var surfaceId = commands[i].SurfaceId;
                if (string.IsNullOrEmpty(surfaceId) || results.Contains(surfaceId))
                {
                    continue;
                }

                results.Add(surfaceId);
            }
        }

        /// <summary>
        /// Replaces the whole history with a list read back off disk, all of it done, and says so
        /// once (ADR 0006).
        /// </summary>
        /// <remarks>
        /// Not a loop of <see cref="Push"/> calls, and the difference is not cosmetic. Every push
        /// raises <see cref="CommandPushed"/>, and a listener answers that by replaying the named
        /// wall's commands onto a cleared canvas — so restoring forty commands on one wall by pushing
        /// them would rasterize that wall forty times, thirty-nine of them into a picture nobody sees.
        /// One notification for the whole list means one redraw per wall (architecture §7).
        /// <para>
        /// It replaces rather than appends, because that is what loading a room means: the file is the
        /// room, not an addition to whatever is already on the walls. Anything undone is dropped with
        /// it — a restore is a new starting point, so there is nothing behind it to redo.
        /// </para>
        /// <para>
        /// Commands are vetted exactly as <see cref="Push"/> vets them: one that is null or names no
        /// surface can never be rendered or re-attached, so it is refused rather than stored
        /// (architecture §8.5). A file that has lost a command that way is still worth restoring the
        /// rest of.
        /// </para>
        /// </remarks>
        /// <returns>How many commands the history now holds.</returns>
        public int Restore(IReadOnlyList<IPaintCommand> restored)
        {
            commands.Clear();
            doneCount = 0;

            var count = restored?.Count ?? 0;
            var refused = 0;

            for (var i = 0; i < count; i++)
            {
                var command = restored[i];

                if (command == null)
                {
                    refused++;
                    continue;
                }

                if (string.IsNullOrEmpty(command.SurfaceId))
                {
                    Debug.LogError(
                        $"[{nameof(PaintHistory)}] Refused a restored {command.GetType().Name} with no " +
                        $"{nameof(IPaintCommand.SurfaceId)}: there is no surface to render it on.", this);
                    refused++;
                    continue;
                }

                commands.Add(command);
            }

            if (refused > 0)
            {
                Debug.LogError(
                    $"[{nameof(PaintHistory)}] Dropped {refused} of {count} restored commands; the rest " +
                    "were kept.", this);
            }

            doneCount = commands.Count;
            HistoryRestored?.Invoke();
            return doneCount;
        }

        private void OnDestroy()
        {
            // Nothing should be holding a torn-down history, and a stale subscriber re-rendering a
            // canvas that is also going away is worse than no notification (architecture §8.4).
            CommandPushed = null;
            CommandUndone = null;
            CommandRedone = null;
            HistoryRestored = null;
            commands.Clear();
            doneCount = 0;
        }
    }
}
