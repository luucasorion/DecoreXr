using System;
using System.Collections.Generic;
using DecoreXR.Core;
using UnityEngine;

namespace DecoreXR.Spatial
{
    /// <summary>
    /// Puts saved paint back on the walls it was painted on, and cleanly leaves behind the paint
    /// whose wall is not in the room (ADR 0006).
    /// </summary>
    /// <remarks>
    /// The half of loading that <see cref="PaintStore"/> deliberately cannot do. A saved command names
    /// an anchor UUID and nothing else; deciding whether that anchor is one of the room's walls means
    /// asking the surface providers, which live here — <c>Core</c> cannot see them (architecture §4),
    /// and ADR 0006 puts anchor-UUID lookup in <c>Spatial</c> for exactly this reason.
    /// <para>
    /// Paint whose anchor is missing is dropped from the history rather than carried in it. It is the
    /// difference between a clean fail and a quiet mess (architecture §8.5): kept, every one of those
    /// commands would be undoable — so "undo the last thing I did" could take back paint the user has
    /// never seen — and <c>PaintRenderer</c> would report a missing surface for each of them. Dropped,
    /// the history holds exactly what is on the walls.
    /// </para>
    /// <para>
    /// Dropped from the history, though, is not deleted from disk. The room the user is standing in is
    /// not the only room they have ever painted, and a re-scan is not the only reason an anchor can be
    /// absent. So the skipped ids are handed to <see cref="PaintStore.Retain"/>, which keeps the next
    /// save from removing their files — otherwise opening the app in the kitchen would erase the
    /// living room. That is the risk ADR 0006 accepted, met by leaving the data alone rather than by
    /// pretending it loaded.
    /// </para>
    /// <para>
    /// It loads once. Re-running it would replace the history, so a second load after the user has
    /// started painting would throw away the session's work — which is why a re-scan is not a reason
    /// to reload, even though it changes which anchors exist. What a re-scan does to canvases already
    /// on screen is <c>PaintRenderer</c>'s to handle, and it already does.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PaintReattacher : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Where the saved rooms are read from.")]
        [SerializeField] private PaintStore store;

        [Tooltip("The history the saved paint is restored into.")]
        [SerializeField] private PaintHistory history;

        [Tooltip("Where the room's walls come from — the MRUK wall provider, the manual-plane " +
                 "provider, or both. Each entry must implement IPaintableSurfaceProvider. These " +
                 "decide which saved anchors are actually in the room and which are skipped.")]
        [SerializeField] private Component[] surfaceProviders = new Component[0];

        [Header("When")]
        [Tooltip("Load as soon as the room's walls are available, without waiting to be asked. Off " +
                 "if the app drives loading itself.")]
        [SerializeField] private bool loadWhenSurfacesAppear = true;

        private readonly List<IPaintableSurfaceProvider> providers = new List<IPaintableSurfaceProvider>();
        private readonly List<SavedCommand> saved = new List<SavedCommand>();
        private readonly List<IPaintCommand> kept = new List<IPaintCommand>();
        private readonly List<string> skippedSurfaces = new List<string>();
        private readonly HashSet<string> available = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>True once the saved room has been loaded, whether or not anything came back.</summary>
        public bool HasLoaded { get; private set; }

        /// <summary>How many commands were put back on walls by the last load.</summary>
        public int ReattachedCount { get; private set; }

        /// <summary>How many commands the last load left behind because their wall was not in the room.</summary>
        public int SkippedCount { get; private set; }

        /// <summary>
        /// The walls the last load left behind, so the app can say how much of the saved room is not
        /// here (architecture §8.5).
        /// </summary>
        public IReadOnlyList<string> SkippedSurfaces => skippedSurfaces;

        /// <summary>Raised after a load, carrying this reattacher so a listener can read the counts.</summary>
        public event Action<PaintReattacher> Loaded;

        private void Reset()
        {
            store = FindAnyObjectByType<PaintStore>();
            history = FindAnyObjectByType<PaintHistory>();
        }

        private void OnEnable()
        {
            if (store == null || history == null)
            {
                Debug.LogError(
                    $"[{nameof(PaintReattacher)}] Needs a {nameof(PaintStore)} and a " +
                    $"{nameof(PaintHistory)}; without them the user's saved room would silently never " +
                    "come back. Assign them in the inspector.", this);
                enabled = false;
                return;
            }

            ResolveProviders();

            if (!loadWhenSurfacesAppear)
            {
                return;
            }

            // The room may already have loaded before this component was enabled, in which case there
            // is no change event still to come.
            if (HasAnySurface())
            {
                Load();
            }
        }

        private void OnDisable()
        {
            for (var i = 0; i < providers.Count; i++)
            {
                providers[i].SurfacesChanged -= OnSurfacesChanged;
            }

            // Re-enabling re-reads the inspector list, so a provider swapped out while disabled is
            // picked up rather than remembered.
            providers.Clear();
        }

        private void OnValidate()
        {
            for (var i = 0; i < surfaceProviders.Length; i++)
            {
                var candidate = surfaceProviders[i];
                if (candidate != null && !(candidate is IPaintableSurfaceProvider))
                {
                    Debug.LogWarning(
                        $"[{nameof(PaintReattacher)}] {candidate.GetType().Name} in slot {i} is not an " +
                        $"{nameof(IPaintableSurfaceProvider)} and will be ignored.", this);
                }
            }
        }

        /// <summary>
        /// Reads the saved rooms and restores the paint whose walls are in this one. Public because the
        /// app may want to ask for it rather than have it happen on its own.
        /// </summary>
        /// <remarks>
        /// Loading a second time is refused rather than done. The history is replaced by a load, so a
        /// reload after the user has painted would throw that painting away — and there is no version
        /// of "load the room again" the user could have meant that includes losing what they just did.
        /// </remarks>
        /// <returns>
        /// True if everything on disk was read and restored. False means part of it was not, and the
        /// reason has been reported — skipping a wall that is not in the room is <em>not</em> such a
        /// case: that is the expected outcome ADR 0006 describes.
        /// </returns>
        public bool Load()
        {
            if (store == null || history == null)
            {
                Debug.LogError(
                    $"[{nameof(PaintReattacher)}] Cannot load without a {nameof(PaintStore)} and a " +
                    $"{nameof(PaintHistory)}.", this);
                return false;
            }

            if (HasLoaded)
            {
                Debug.LogWarning(
                    $"[{nameof(PaintReattacher)}] Already loaded; ignoring a second request. Loading " +
                    "replaces the history, so doing it again would discard whatever has been painted " +
                    "since.", this);
                return false;
            }

            // Refused rather than attempted. With nothing to ask about the room, every saved wall
            // looks absent and the load would "succeed" with an empty history — and, because loading
            // happens once, that wrong answer would be the final one. A room with no walls in it is a
            // different thing entirely and is allowed through below (architecture §8.5).
            if (providers.Count == 0)
            {
                Debug.LogError(
                    $"[{nameof(PaintReattacher)}] Not loading: no {nameof(IPaintableSurfaceProvider)} " +
                    "is resolved, so every saved wall would look like one that is not in the room and " +
                    "the user's paint would be silently left behind.", this);
                return false;
            }

            HasLoaded = true;

            var readEverything = store.Load(saved);

            CollectAvailableSurfaces();

            kept.Clear();
            skippedSurfaces.Clear();
            var skippedCommands = 0;

            for (var i = 0; i < saved.Count; i++)
            {
                var command = saved[i].Command;
                if (command == null)
                {
                    continue;
                }

                if (available.Contains(command.SurfaceId))
                {
                    kept.Add(command);
                    continue;
                }

                skippedCommands++;
                if (!skippedSurfaces.Contains(command.SurfaceId))
                {
                    skippedSurfaces.Add(command.SurfaceId);
                }
            }

            SkippedCount = skippedCommands;

            // Marked before the history is restored, not after, and the order is the point. Loading
            // is what lets the store start deleting the files of walls with no paint on them, and
            // restoring raises an event synchronously — so between the two calls there would be a
            // moment when the store considered itself loaded while nothing was yet marked as another
            // room's. A listener that saved in that moment would delete exactly the files this call
            // exists to protect. No listener does today; doing it in this order means none can.
            store.Retain(skippedSurfaces);

            ReattachedCount = history.Restore(kept);

            Report();

            saved.Clear();
            kept.Clear();

            Loaded?.Invoke(this);
            return readEverything;
        }

        /// <summary>
        /// Says what came back and what did not, once, in a line the user's problem can be read out
        /// of: paint that is not on the walls has to be accounted for rather than just missing
        /// (architecture §8.5).
        /// </summary>
        private void Report()
        {
            if (ReattachedCount == 0 && SkippedCount == 0)
            {
                // A first launch, or a room nobody has painted. Nothing to say.
                return;
            }

            if (SkippedCount == 0)
            {
                Debug.Log(
                    $"[{nameof(PaintReattacher)}] Restored {ReattachedCount} paint commands onto " +
                    "this room's walls.", this);
                return;
            }

            Debug.LogWarning(
                $"[{nameof(PaintReattacher)}] Restored {ReattachedCount} paint commands; left " +
                $"{SkippedCount} on {skippedSurfaces.Count} wall(s) that are not in this room. Their " +
                "saved paint is kept on disk and will come back if those anchors do — re-running " +
                "Space Setup replaces a room's anchors, which is when this happens (ADR 0006).", this);
        }

        private void CollectAvailableSurfaces()
        {
            available.Clear();

            for (var p = 0; p < providers.Count; p++)
            {
                var surfaces = providers[p].Surfaces;
                for (var s = 0; s < surfaces.Count; s++)
                {
                    var surface = surfaces[s];

                    // An id-less surface cannot match saved paint at all: a manual plane whose spatial
                    // anchor never materialised has none (ADR 0010), and nothing was ever saved under
                    // it either (ADR 0006).
                    if (surface != null && surface.IsValid && !string.IsNullOrEmpty(surface.Id))
                    {
                        available.Add(surface.Id);
                    }
                }
            }
        }

        private bool HasAnySurface()
        {
            for (var p = 0; p < providers.Count; p++)
            {
                if (providers[p].Surfaces.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The room's walls have arrived, so the saved paint can be matched against them. Only the
        /// first such moment matters; after that the history is the session's, not the file's.
        /// </summary>
        private void OnSurfacesChanged(IPaintableSurfaceProvider provider)
        {
            if (HasLoaded || !loadWhenSurfacesAppear || !HasAnySurface())
            {
                return;
            }

            Load();
        }

        private void ResolveProviders()
        {
            providers.Clear();

            for (var i = 0; i < surfaceProviders.Length; i++)
            {
                if (surfaceProviders[i] is IPaintableSurfaceProvider provider)
                {
                    providers.Add(provider);
                    provider.SurfacesChanged += OnSurfacesChanged;
                }
            }

            if (providers.Count == 0)
            {
                Debug.LogError(
                    $"[{nameof(PaintReattacher)}] No {nameof(IPaintableSurfaceProvider)} assigned, so " +
                    "every saved command would look like paint on a wall that is not here and the " +
                    "user's room would never come back. Assign the wall and/or manual-plane provider " +
                    "in the inspector.", this);
            }
        }
    }
}
