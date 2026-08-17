using System;
using System.Collections.Generic;
using UnityEngine;

namespace DecoreXR.Core
{
    /// <summary>
    /// The one list of everything the user has painted, in the order they did it, across every wall
    /// in the room (ADR 0007). Pushing to it is how a paint action becomes real: the canvases are
    /// rendered from this, never the other way round (ADR 0003).
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
    /// M3 only pushes. Undo and redo are M7's job (ADR 0007), and writing this list to disk under
    /// each anchor UUID is M8's (ADR 0006); both read the same append-ordered list, so neither needs
    /// this to change shape.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PaintHistory : MonoBehaviour
    {
        private readonly List<IPaintCommand> commands = new List<IPaintCommand>();

        /// <summary>How many commands have been pushed.</summary>
        public int Count => commands.Count;

        /// <summary>
        /// Raised after a command is accepted, carrying it so a listener can re-render just the one
        /// surface it names instead of every canvas in the room (architecture §4).
        /// </summary>
        public event Action<IPaintCommand> CommandPushed;

        /// <summary>
        /// Records a command as done. Rejects a null command, or one that names no surface, with an
        /// error rather than storing it: such a command can never be rendered or re-attached on load,
        /// so keeping it would turn a wiring mistake into paint that silently never appears
        /// (architecture §8.5).
        /// </summary>
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

            commands.Add(command);
            CommandPushed?.Invoke(command);
            return true;
        }

        /// <summary>
        /// Appends every command targeting <paramref name="surfaceId"/> to <paramref name="results"/>,
        /// oldest first, which is the order a canvas has to draw them in for later paint to land on
        /// top of earlier paint.
        /// </summary>
        /// <remarks>
        /// Fills a caller-owned list instead of returning one so that re-rendering a wall reuses a
        /// buffer rather than allocating per render (architecture §7). The list is cleared first, so
        /// the caller gets that wall's commands and nothing else.
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
            for (var i = 0; i < commands.Count; i++)
            {
                if (string.Equals(commands[i].SurfaceId, surfaceId, StringComparison.Ordinal))
                {
                    results.Add(commands[i]);
                }
            }
        }

        private void OnDestroy()
        {
            // Nothing should be holding a torn-down history, and a stale subscriber re-rendering a
            // canvas that is also going away is worse than no notification (architecture §8.4).
            CommandPushed = null;
            commands.Clear();
        }
    }
}
