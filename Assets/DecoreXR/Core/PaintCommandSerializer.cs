using System;
using System.Collections.Generic;
using UnityEngine;

namespace DecoreXR.Core
{
    /// <summary>
    /// One wall's command list as JSON text, and back (ADR 0006). Text only — where that text is
    /// kept is <see cref="PaintStore"/>'s business.
    /// </summary>
    /// <remarks>
    /// The file holds the wall's id once and then its commands in order, each entry saying which
    /// kind of command it is, where it stood in the global history, and its own data:
    /// <code>
    /// {"version":1,"surfaceId":"anchor-uuid","commands":[{"order":0,"type":"fill","data":"..."}]}
    /// </code>
    /// The payload is a nested JSON string rather than a nested JSON object because
    /// <see cref="JsonUtility"/> — Unity's own serializer, and so the one choice here that adds no
    /// dependency — has no notion of a polymorphic field: it deserializes into a type known at
    /// compile time, and <c>Core</c> is precisely the place that must not know the command types
    /// (architecture §4). Giving each codec a string of its own to fill is what buys the
    /// per-command-type seam <see cref="IPaintCommandCodec"/> describes. The cost is one level of
    /// escaping in a file no user reads.
    /// <para>
    /// Reading is deliberately lenient about entries and strict about files. A whole file that
    /// cannot be understood is refused, because guessing at it would restore a wall to something the
    /// user never painted. A single entry that cannot be — an unknown tag from a build with a tool
    /// this one lacks, or a payload that will not parse — is skipped and reported, because losing one
    /// shape is better than losing the room (architecture §8.5).
    /// </para>
    /// </remarks>
    public sealed class PaintCommandSerializer
    {
        /// <summary>
        /// The format the writer produces. A file from a newer format is refused rather than read as
        /// if it were this one.
        /// </summary>
        public const int FormatVersion = 1;

        private readonly PaintCommandCodecRegistry codecs;

        public PaintCommandSerializer(PaintCommandCodecRegistry codecs)
        {
            this.codecs = codecs;
        }

        /// <summary>
        /// Writes one surface's commands, in the order given.
        /// </summary>
        /// <param name="surfaceId">The wall's anchor UUID — the file's key (ADR 0006).</param>
        /// <param name="commands">
        /// That wall's commands with their places in the global history. Commands naming another
        /// surface are skipped: the file says its surface once, so an entry that disagreed with it
        /// would come back attached to the wrong wall.
        /// </param>
        /// <returns>The file's text, or null if there was nothing writable to write.</returns>
        public string Write(string surfaceId, IReadOnlyList<SavedCommand> commands)
        {
            if (codecs == null)
            {
                Debug.LogError($"[{nameof(PaintCommandSerializer)}] No codec registry, so nothing can be written.");
                return null;
            }

            if (string.IsNullOrEmpty(surfaceId))
            {
                Debug.LogError(
                    $"[{nameof(PaintCommandSerializer)}] Refused to write a file with no surface id: " +
                    "nothing could re-attach it to a wall on load.");
                return null;
            }

            var count = commands?.Count ?? 0;
            if (count == 0)
            {
                return null;
            }

            var entries = new List<PaintEntryDto>(count);

            for (var i = 0; i < count; i++)
            {
                var saved = commands[i];
                var command = saved.Command;

                if (command == null)
                {
                    continue;
                }

                if (!string.Equals(command.SurfaceId, surfaceId, StringComparison.Ordinal))
                {
                    Debug.LogError(
                        $"[{nameof(PaintCommandSerializer)}] Skipped a {command.GetType().Name} naming " +
                        $"surface {Quoted(command.SurfaceId)} while writing {Quoted(surfaceId)}.");
                    continue;
                }

                if (!codecs.TryGetFor(command, out var codec))
                {
                    Debug.LogError(
                        $"[{nameof(PaintCommandSerializer)}] No codec writes {command.GetType().Name}, " +
                        "so that paint would not survive a restart. Register one with the " +
                        $"{nameof(PaintStore)} (ADR 0006).");
                    continue;
                }

                string payload;
                try
                {
                    payload = codec.Write(command);
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[{nameof(PaintCommandSerializer)}] Codec {Quoted(codec.TypeId)} failed writing " +
                        $"a {command.GetType().Name}: {e.Message}");
                    continue;
                }

                if (payload == null)
                {
                    Debug.LogError(
                        $"[{nameof(PaintCommandSerializer)}] Codec {Quoted(codec.TypeId)} wrote nothing " +
                        $"for a {command.GetType().Name}, so that command is not saved.");
                    continue;
                }

                entries.Add(new PaintEntryDto
                {
                    order = saved.Order,
                    type = codec.TypeId,
                    data = payload,
                });
            }

            if (entries.Count == 0)
            {
                return null;
            }

            var file = new PaintFileDto
            {
                version = FormatVersion,
                surfaceId = surfaceId,
                commands = entries.ToArray(),
            };

            return JsonUtility.ToJson(file);
        }

        /// <summary>
        /// Reads a file back into commands, appending them to <paramref name="into"/> in the order
        /// they appear.
        /// </summary>
        /// <param name="json">The file's text.</param>
        /// <param name="into">
        /// Caller-owned list the commands are appended to — not cleared, so a caller merging every
        /// wall's file into one history reads them all into the same list.
        /// </param>
        /// <param name="surfaceId">The wall the file belongs to, or empty when it could not be read.</param>
        /// <returns>
        /// True if the file was understood. Entries inside it may still have been skipped; those are
        /// reported individually.
        /// </returns>
        public bool TryRead(string json, List<SavedCommand> into, out string surfaceId)
        {
            surfaceId = string.Empty;

            if (into == null)
            {
                Debug.LogError($"[{nameof(PaintCommandSerializer)}] {nameof(TryRead)} needs a list to fill.");
                return false;
            }

            if (codecs == null)
            {
                Debug.LogError($"[{nameof(PaintCommandSerializer)}] No codec registry, so nothing can be read.");
                return false;
            }

            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            PaintFileDto file;
            try
            {
                file = JsonUtility.FromJson<PaintFileDto>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(PaintCommandSerializer)}] Could not parse a paint file: {e.Message}");
                return false;
            }

            if (file == null)
            {
                Debug.LogError($"[{nameof(PaintCommandSerializer)}] A paint file held no paint data.");
                return false;
            }

            if (file.version > FormatVersion)
            {
                // Refused, not guessed at. A newer format may mean something different by the same
                // field, and half-restoring a room is worse than saying the file is not ours to read.
                Debug.LogError(
                    $"[{nameof(PaintCommandSerializer)}] A paint file is format {file.version}, newer " +
                    $"than this build reads ({FormatVersion}). It is left alone rather than misread.");
                return false;
            }

            if (string.IsNullOrEmpty(file.surfaceId))
            {
                Debug.LogError(
                    $"[{nameof(PaintCommandSerializer)}] A paint file names no surface, so its paint " +
                    "could not be put on a wall.");
                return false;
            }

            surfaceId = file.surfaceId;

            var entries = file.commands;
            if (entries == null)
            {
                return true;
            }

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (!codecs.TryGetByTypeId(entry.type, out var codec))
                {
                    Debug.LogWarning(
                        $"[{nameof(PaintCommandSerializer)}] Skipped an entry tagged " +
                        $"{Quoted(entry.type)} on surface {Quoted(surfaceId)}: this build has no tool " +
                        "that reads it.");
                    continue;
                }

                IPaintCommand command;
                try
                {
                    command = codec.Read(surfaceId, entry.data);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        $"[{nameof(PaintCommandSerializer)}] Codec {Quoted(codec.TypeId)} could not read " +
                        $"an entry on surface {Quoted(surfaceId)}: {e.Message}");
                    continue;
                }

                if (command == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(PaintCommandSerializer)}] Codec {Quoted(codec.TypeId)} made nothing of " +
                        $"an entry on surface {Quoted(surfaceId)}, so that command is not restored.");
                    continue;
                }

                into.Add(new SavedCommand(entry.order, command));
            }

            return true;
        }

        /// <summary>
        /// Wraps a value in quotes for a message, so an empty or whitespace id is visible in the
        /// console rather than reading as a gap in the sentence.
        /// </summary>
        private static string Quoted(string value) => value == null ? "<null>" : "'" + value + "'";

        /// <summary>
        /// One wall's file. Serializable shapes rather than hand-built JSON so that escaping,
        /// unicode and the nested payload are <see cref="JsonUtility"/>'s problem and not ours.
        /// </summary>
        [Serializable]
        private sealed class PaintFileDto
        {
            public int version;
            public string surfaceId;
            public PaintEntryDto[] commands;
        }

        /// <summary>One command in a wall's file.</summary>
        [Serializable]
        private sealed class PaintEntryDto
        {
            public int order;
            public string type;
            public string data;
        }
    }
}
