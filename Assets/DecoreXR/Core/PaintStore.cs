using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DecoreXR.Core
{
    /// <summary>
    /// Where the room's paint lives between sessions: one JSON file per wall, named for that wall's
    /// spatial anchor UUID, under <see cref="Application.persistentDataPath"/> (ADR 0006).
    /// </summary>
    /// <remarks>
    /// A file per anchor rather than one file for the room, because the anchor UUID is the key
    /// ADR 0006 chose and the filename is the most direct way to be keyed by it: loading a wall is
    /// then a lookup rather than a scan, a wall the user paints over rewrites only its own file, and
    /// a file that gets corrupted costs that wall instead of the room. It is also what makes
    /// "the anchor is gone" cheap to act on — the orphaned data is one file, sitting under a name
    /// nothing in the room answers to any more.
    /// <para>
    /// Only <em>done</em> commands are written. The file is what the walls look like, and ADR 0007
    /// promises undo within a session, not across a restart; carrying the undone tail to disk would
    /// mean a redo button live on launch offering to put back paint from a session the user has
    /// already left.
    /// </para>
    /// <para>
    /// A wall with no paint on it loses its file, which is how a user who undoes a wall back to bare
    /// has that stick. A wall whose <em>anchor</em> is not in the current room looks identical from
    /// here and must not be treated the same, so whoever loads marks those with <see cref="Retain"/>
    /// and their files are left exactly as they are — otherwise launching the app in one room would
    /// erase another (ADR 0006 names orphaned anchors as its risk; erasing them is not the clean fail
    /// architecture §8.5 asks for).
    /// </para>
    /// <para>
    /// This class saves and loads text. It does not put loaded paint back on walls — that has to know
    /// which walls are actually in the room, which is <c>Spatial</c>'s to answer and
    /// <c>PaintReattacher</c>'s to do. Keeping the two apart is also what lets the store be exercised
    /// with no room scanned at all.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PaintStore : MonoBehaviour
    {
        /// <summary>Folder used when the inspector field is left blank.</summary>
        private const string DefaultFolderName = "paint";

        private const string FileExtension = ".json";

        // Written to first and renamed into place, so a save interrupted by the OS reclaiming the app
        // leaves the previous file intact rather than a half-written one.
        private const string TempExtension = ".tmp";

        [Header("Command types")]
        [Tooltip("Where the codecs come from — normally the Painting assembly's codec source. Each " +
                 "entry must implement IPaintCommandCodecSource. Core cannot name the command types " +
                 "it persists, so this is how it learns to read and write them (ADR 0006).")]
        [SerializeField] private Component[] codecSources = new Component[0];

        [Header("Location")]
        [Tooltip("Folder under Application.persistentDataPath holding one file per painted wall. " +
                 "Kept a field rather than a constant so a build can be pointed at a fresh folder " +
                 "without invalidating what is already saved.")]
        [SerializeField] private string folderName = DefaultFolderName;

        private readonly PaintCommandCodecRegistry codecs = new PaintCommandCodecRegistry();
        private readonly List<IPaintCommand> done = new List<IPaintCommand>();
        private readonly Dictionary<string, List<SavedCommand>> bySurface =
            new Dictionary<string, List<SavedCommand>>(StringComparer.Ordinal);
        private readonly List<SavedCommand> fromFile = new List<SavedCommand>();
        private readonly HashSet<string> retained = new HashSet<string>(StringComparer.Ordinal);

        private PaintCommandSerializer serializer;
        private bool codecsResolved;
        private bool hasLoaded;
        private bool warnedAboutDeletingBeforeLoad;

        /// <summary>
        /// The folder the paint files sit in. Built on demand rather than cached: on Android
        /// <see cref="Application.persistentDataPath"/> is only meaningful once the player is up.
        /// </summary>
        public string DirectoryPath =>
            Path.Combine(
                Application.persistentDataPath,
                string.IsNullOrWhiteSpace(folderName) ? DefaultFolderName : folderName.Trim());

        /// <summary>
        /// Whether this store has read the disk this session — which is what makes deleting a file
        /// safe.
        /// </summary>
        /// <remarks>
        /// Until a load has happened, "this wall has no paint in the history" and "nobody has looked
        /// at what is saved yet" are the same observation from in here, and only one of them means the
        /// file should go. So <see cref="Save"/> writes but removes nothing until then.
        /// </remarks>
        public bool HasLoaded => hasLoaded;

        /// <summary>How many command types this store can read and write.</summary>
        public int CodecCount
        {
            get
            {
                EnsureCodecs();
                return codecs.Count;
            }
        }

        private void OnEnable()
        {
            // Resolved eagerly so a wiring mistake is reported on startup rather than at the moment
            // the user's paint fails to save (architecture §8.5).
            EnsureCodecs();
        }

        private void OnDisable()
        {
            // Re-enabling re-reads the inspector list, so a codec source swapped out while disabled
            // is picked up rather than remembered.
            codecsResolved = false;
        }

        private void OnValidate()
        {
            for (var i = 0; i < codecSources.Length; i++)
            {
                var candidate = codecSources[i];
                if (candidate != null && !(candidate is IPaintCommandCodecSource))
                {
                    Debug.LogWarning(
                        $"[{nameof(PaintStore)}] {candidate.GetType().Name} in slot {i} is not an " +
                        $"{nameof(IPaintCommandCodecSource)} and will be ignored.", this);
                }
            }
        }

        /// <summary>
        /// Writes every done command in <paramref name="history"/> to disk, one file per wall, and
        /// removes the file of any wall that no longer has paint on it.
        /// </summary>
        /// <remarks>
        /// Deleting is part of saving, not a separate tidy-up. A user who undoes a wall back to bare
        /// and then closes the app has said that wall is bare; a file left behind would put the paint
        /// back on next launch, which is the opposite of what they did.
        /// </remarks>
        /// <returns>
        /// True if the whole save went through. False means some of it did not, and the reason has
        /// been reported — the caller should not tell the user their room is safe.
        /// </returns>
        public bool Save(PaintHistory history)
        {
            EnsureCodecs();

            if (history == null)
            {
                Debug.LogError(
                    $"[{nameof(PaintStore)}] Nothing to save: no {nameof(PaintHistory)} was given.", this);
                return false;
            }

            if (codecs.Count == 0)
            {
                Debug.LogError(
                    $"[{nameof(PaintStore)}] No codecs, so no command could be written and the room " +
                    $"would be lost on exit. Assign an {nameof(IPaintCommandCodecSource)} in the " +
                    "inspector.", this);
                return false;
            }

            GroupBySurface(history);

            var directory = DirectoryPath;

            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[{nameof(PaintStore)}] Could not open '{directory}' to save the room: {e.Message}", this);
                return false;
            }

            var ok = true;

            foreach (var entry in bySurface)
            {
                var json = serializer.Write(entry.Key, entry.Value);
                if (json == null)
                {
                    // Reported by the serializer. The wall stays in the group, so whatever was
                    // already on disk for it is left alone rather than deleted: paint we failed to
                    // write down is better represented by the last save than by nothing.
                    ok = false;
                    continue;
                }

                if (!WriteFile(entry.Key, json))
                {
                    ok = false;
                }
            }

            return DeleteFilesForUnpaintedSurfaces(directory) && ok;
        }

        /// <summary>
        /// Reads every saved wall back, appending the commands to <paramref name="into"/> in the
        /// order they were originally done — across walls, not just within each one.
        /// </summary>
        /// <remarks>
        /// The merge is the whole reason a command carries its place in the history to disk
        /// (see <see cref="SavedCommand.Order"/>). Read file by file, the room would come back
        /// grouped by wall, and the first undo after a restart would take back the last thing done on
        /// whichever wall was read last rather than the last thing the user did (ADR 0007).
        /// <para>
        /// Nothing saved yet is success with an empty list, not a failure: a first launch has no
        /// files and that is not something to tell the user about.
        /// </para>
        /// </remarks>
        /// <param name="into">Caller-owned list, cleared first and filled with what was on disk.</param>
        /// <returns>True if everything on disk was read. False means part of it was not, and why has been reported.</returns>
        public bool Load(List<SavedCommand> into)
        {
            EnsureCodecs();

            if (into == null)
            {
                Debug.LogError($"[{nameof(PaintStore)}] {nameof(Load)} needs a list to fill.", this);
                return false;
            }

            into.Clear();

            // Each load re-decides which walls are orphaned, so the previous load's answer is not
            // inherited (see Retain).
            retained.Clear();

            // Set before the early returns below: a first launch with nothing saved is still a session
            // that has looked, and a wall painted and then undone in it should lose the file it just
            // got (see HasLoaded).
            hasLoaded = true;

            if (codecs.Count == 0)
            {
                Debug.LogError(
                    $"[{nameof(PaintStore)}] No codecs, so nothing saved could be read back. Assign an " +
                    $"{nameof(IPaintCommandCodecSource)} in the inspector.", this);
                return false;
            }

            var directory = DirectoryPath;
            if (!Directory.Exists(directory))
            {
                return true;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(directory, "*" + FileExtension);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[{nameof(PaintStore)}] Could not list '{directory}': {e.Message}", this);
                return false;
            }

            var ok = true;

            for (var i = 0; i < files.Length; i++)
            {
                string json;
                try
                {
                    json = File.ReadAllText(files[i]);
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[{nameof(PaintStore)}] Could not read '{files[i]}': {e.Message}", this);
                    ok = false;
                    continue;
                }

                fromFile.Clear();
                if (!serializer.TryRead(json, fromFile, out var surfaceId))
                {
                    // The serializer already said what was wrong with it.
                    ok = false;
                    continue;
                }

                // The file's own contents win over its name. A command carries its surface id, so a
                // file that was copied or renamed still restores onto the wall it was painted on —
                // but the mismatch means this wall will not be found by name again, and that is worth
                // knowing about.
                var expectedName = Path.GetFileNameWithoutExtension(files[i]);
                if (!string.Equals(expectedName, surfaceId, StringComparison.Ordinal))
                {
                    Debug.LogWarning(
                        $"[{nameof(PaintStore)}] '{files[i]}' holds paint for surface '{surfaceId}'. " +
                        "Its contents are used; the file will be rewritten under the right name on the " +
                        "next save.", this);
                }

                into.AddRange(fromFile);
            }

            fromFile.Clear();

            // Sorted once here rather than merged as the files are read: the whole point is a single
            // globally ordered list, and the caller must not have to know it needs sorting.
            into.Sort(CompareByOrder);
            return ok;
        }

        /// <summary>
        /// Marks walls whose saved paint is on disk but not in the history, so that saving does not
        /// delete their files.
        /// </summary>
        /// <remarks>
        /// This is what stops the app from destroying a room the user is not standing in. Saving
        /// removes the file of any wall with no paint on it, which is right for a wall the user undid
        /// back to bare — but a wall whose anchor is simply not in the current room has no paint in
        /// the history for exactly the opposite reason, and looks identical from here. A user with two
        /// rooms saved would otherwise lose the first one by launching the app in the second
        /// (ADR 0006 names orphaned anchors as its risk; erasing them is not the clean fail
        /// architecture §8.5 asks for).
        /// <para>
        /// Whoever loads decides, because deciding needs to know which anchors the room actually has
        /// and <c>Core</c> cannot ask (architecture §4). <see cref="Load"/> clears the marks first, so
        /// each load re-decides rather than inheriting the last one's answer.
        /// </para>
        /// </remarks>
        public void Retain(IReadOnlyList<string> surfaceIds)
        {
            if (surfaceIds == null)
            {
                return;
            }

            for (var i = 0; i < surfaceIds.Count; i++)
            {
                if (!string.IsNullOrEmpty(surfaceIds[i]))
                {
                    retained.Add(surfaceIds[i]);
                }
            }
        }

        /// <summary>
        /// Whether saving will leave <paramref name="surfaceId"/>'s file alone even with no paint on
        /// it in the history.
        /// </summary>
        public bool IsRetained(string surfaceId) =>
            !string.IsNullOrEmpty(surfaceId) && retained.Contains(surfaceId);

        /// <summary>
        /// Buckets the history's done commands by wall, remembering each one's place in the global
        /// list so a load can put them back in that order.
        /// </summary>
        private void GroupBySurface(PaintHistory history)
        {
            bySurface.Clear();
            history.CollectDone(done);

            for (var i = 0; i < done.Count; i++)
            {
                var command = done[i];
                if (command == null)
                {
                    continue;
                }

                var surfaceId = command.SurfaceId;

                // PaintHistory refuses a command with no surface, so this is belt and braces — but a
                // command with an unusable one is a real case: a manual plane whose spatial anchor
                // never materialised has an empty id (ADR 0010), and its paint simply is not
                // persistable (ADR 0006).
                if (string.IsNullOrEmpty(surfaceId))
                {
                    continue;
                }

                if (!IsUsableFileName(surfaceId))
                {
                    Debug.LogError(
                        $"[{nameof(PaintStore)}] Surface id '{surfaceId}' cannot be a file name, so " +
                        "that wall's paint is not saved. Anchor UUIDs always can; this one did not " +
                        "come from an anchor.", this);
                    continue;
                }

                if (!bySurface.TryGetValue(surfaceId, out var forSurface))
                {
                    forSurface = new List<SavedCommand>();
                    bySurface.Add(surfaceId, forSurface);
                }

                forSurface.Add(new SavedCommand(i, command));
            }

            done.Clear();
        }

        /// <summary>
        /// Writes one wall's file, via a temporary name so the previous one survives a save that does
        /// not finish.
        /// </summary>
        private bool WriteFile(string surfaceId, string json)
        {
            var path = PathFor(surfaceId);
            var temp = path + TempExtension;

            try
            {
                File.WriteAllText(temp, json);

                // Move rather than replace: File.Replace is not dependable on Android, and Move will
                // not overwrite, so the old file goes first. The gap between the two is the one moment
                // a kill would lose this wall — which is why the write above happens before it and
                // not into the real file.
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[{nameof(PaintStore)}] Could not save surface '{surfaceId}' to '{path}': " +
                    $"{e.Message}", this);

                // Leaving a stray .tmp behind would make the next save think a write is in flight.
                TryDelete(temp);
                return false;
            }
        }

        /// <summary>
        /// Removes the files of walls that have no paint on them any more — undone back to bare, or
        /// painted in a room that has since been re-scanned.
        /// </summary>
        private bool DeleteFilesForUnpaintedSurfaces(string directory)
        {
            // Nothing is removed before a load, and this is the guard that makes Retain trustworthy
            // rather than merely usually-applied. Retain can only mark a wall as another room's once
            // somebody has read the disk and matched it against this room; asked to save before that,
            // every saved wall looks like a bare one and the rule below would delete the lot. Writing
            // is always safe, so a save that cannot tidy up still saves (ADR 0006, architecture §8.5).
            if (!hasLoaded)
            {
                if (!warnedAboutDeletingBeforeLoad)
                {
                    warnedAboutDeletingBeforeLoad = true;
                    Debug.LogWarning(
                        $"[{nameof(PaintStore)}] Saved without having loaded first, so no saved wall " +
                        "was removed. Until the saved rooms have been read, a wall with no paint on it " +
                        "cannot be told apart from one belonging to a room that is not here, and " +
                        "deleting it would lose the user's paint.", this);
                }

                return true;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(directory, "*" + FileExtension);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[{nameof(PaintStore)}] Saved the room but could not check '{directory}' for " +
                    $"files to remove: {e.Message}", this);
                return false;
            }

            var ok = true;

            for (var i = 0; i < files.Length; i++)
            {
                var surfaceId = Path.GetFileNameWithoutExtension(files[i]);
                if (bySurface.ContainsKey(surfaceId))
                {
                    continue;
                }

                // Painted, but on a wall that is not in this room. That file is another room's and
                // stays exactly as it is (see Retain).
                if (retained.Contains(surfaceId))
                {
                    continue;
                }

                try
                {
                    File.Delete(files[i]);
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[{nameof(PaintStore)}] Could not remove '{files[i]}', so surface " +
                        $"'{surfaceId}' will come back painted next launch: {e.Message}", this);
                    ok = false;
                }
            }

            return ok;
        }

        private string PathFor(string surfaceId) =>
            Path.Combine(DirectoryPath, surfaceId + FileExtension);

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // Already reporting the failure that got us here; a second message about the leftover
                // would only bury it.
            }
        }

        /// <summary>
        /// Whether an id can be a file name as it stands. Never sanitised into one: two different
        /// anchors could clean up to the same name and quietly share a file, and paint appearing on
        /// the wrong wall is worse than paint that was not saved (architecture §8.5).
        /// </summary>
        private static bool IsUsableFileName(string surfaceId) =>
            surfaceId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
            surfaceId != "." &&
            surfaceId != "..";

        private static int CompareByOrder(SavedCommand a, SavedCommand b) => a.Order.CompareTo(b.Order);

        private void EnsureCodecs()
        {
            if (codecsResolved)
            {
                return;
            }

            codecsResolved = true;

            // Emptied first: re-enabling re-reads the inspector list, and re-registering a codec the
            // registry already holds is refused as a collision — so without this, a store that is
            // disabled and enabled again would report its own wiring as broken.
            codecs.Clear();
            serializer = new PaintCommandSerializer(codecs);

            for (var i = 0; i < codecSources.Length; i++)
            {
                if (!(codecSources[i] is IPaintCommandCodecSource source))
                {
                    continue;
                }

                var offered = source.Codecs;
                if (offered == null)
                {
                    continue;
                }

                for (var c = 0; c < offered.Count; c++)
                {
                    codecs.Register(offered[c]);
                }
            }

            if (codecs.Count == 0)
            {
                Debug.LogError(
                    $"[{nameof(PaintStore)}] No {nameof(IPaintCommandCodecSource)} assigned, so no " +
                    "command type can be written or read and the user's room will not survive a " +
                    "restart (ADR 0006). Assign one in the inspector.", this);
            }
        }
    }
}
