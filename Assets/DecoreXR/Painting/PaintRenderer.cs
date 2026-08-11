using System.Collections.Generic;
using DecoreXR.Core;
using DecoreXR.Spatial;
using UnityEngine;

namespace DecoreXR.Painting
{
    /// <summary>
    /// Keeps the canvases showing what the command history says. It watches
    /// <see cref="PaintHistory"/>, and when a command lands it re-renders the one surface that
    /// command names — never the whole room (architecture §4).
    /// </summary>
    /// <remarks>
    /// A re-render replays that surface's commands from scratch onto a cleared canvas rather than
    /// drawing the new command on top of what was already there. Both give the same picture today,
    /// but only the replay keeps the history as the actual source of truth (ADR 0003): it is the same
    /// path M7's undo needs, where the last command has to disappear, and the same path M8 needs when
    /// paint is reloaded from disk with no canvas to draw on top of.
    /// <para>
    /// A canvas is created the first time a surface is painted, so an untouched wall costs nothing —
    /// no texture, and no transparent quad adding fill rate to a budget shared with passthrough
    /// (ADR 0004).
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PaintRenderer : MonoBehaviour
    {
        [Header("Source of truth")]
        [Tooltip("The command history this renderer reflects.")]
        [SerializeField] private PaintHistory history;

        [Header("Canvases")]
        [Tooltip("The PaintCanvas prefab instantiated once per painted surface.")]
        [SerializeField] private PaintCanvas canvasPrefab;

        [Tooltip("Canvas resolution budget (ADR 0004). The per-platform Active budget is used, so " +
                 "quality scales by config alone (ADR 0012).")]
        [SerializeField] private QualityBudgetConfig qualityBudget;

        [Header("Surfaces")]
        [Tooltip("Where paintable surfaces come from — the MRUK wall provider, the manual-plane " +
                 "provider, or both. Each entry must implement IPaintableSurfaceProvider. A command " +
                 "carries only a surface id, so these are how that id is turned back into a wall.")]
        [SerializeField] private Component[] surfaceProviders = new Component[0];

        private readonly Dictionary<string, PaintCanvas> canvases = new Dictionary<string, PaintCanvas>();
        private readonly List<IPaintableSurfaceProvider> providers = new List<IPaintableSurfaceProvider>();
        private readonly List<IPaintCommand> replay = new List<IPaintCommand>();
        private readonly List<string> pruned = new List<string>();

        // Surfaces already reported as missing. A wall that is not in the room is re-checked on every
        // command pushed against it, and from M6 that is many commands a second — the situation is
        // worth saying once, not once per stroke.
        private readonly HashSet<string> reportedMissing = new HashSet<string>();

        private void OnEnable()
        {
            if (history == null || canvasPrefab == null || qualityBudget == null)
            {
                Debug.LogError(
                    $"[{nameof(PaintRenderer)}] Needs a {nameof(PaintHistory)}, a canvas prefab and a " +
                    $"{nameof(QualityBudgetConfig)}. Without them paint would be recorded and never " +
                    "appear. Assign them in the inspector.", this);
                enabled = false;
                return;
            }

            ResolveProviders();
            history.CommandPushed += OnCommandPushed;
        }

        private void OnDisable()
        {
            if (history != null)
            {
                history.CommandPushed -= OnCommandPushed;
            }

            for (var i = 0; i < providers.Count; i++)
            {
                providers[i].SurfacesChanged -= OnSurfacesChanged;
            }

            // Re-enabling re-reads the inspector list, so a provider swapped out while disabled is
            // picked up rather than remembered.
            providers.Clear();
        }

        private void OnDestroy()
        {
            // The canvases are this renderer's to destroy: they are deliberately not parented to it
            // (see CreateCanvas), so nothing else will clean them up (architecture §8.4).
            foreach (var canvas in canvases.Values)
            {
                if (canvas != null)
                {
                    Destroy(canvas.gameObject);
                }
            }

            canvases.Clear();
        }

        private void OnValidate()
        {
            for (var i = 0; i < surfaceProviders.Length; i++)
            {
                var candidate = surfaceProviders[i];
                if (candidate != null && !(candidate is IPaintableSurfaceProvider))
                {
                    Debug.LogWarning(
                        $"[{nameof(PaintRenderer)}] {candidate.GetType().Name} in slot {i} is not an " +
                        $"{nameof(IPaintableSurfaceProvider)} and will be ignored.", this);
                }
            }
        }

        /// <summary>
        /// Re-renders one surface from its commands. Public because undo/redo needs exactly this and
        /// nothing more (ADR 0007, M7).
        /// </summary>
        public void Redraw(string surfaceId)
        {
            if (string.IsNullOrEmpty(surfaceId))
            {
                return;
            }

            var canvas = GetOrCreateCanvas(surfaceId);
            if (canvas == null)
            {
                return;
            }

            history.CollectFor(surfaceId, replay);

            canvas.Clear();
            for (var i = 0; i < replay.Count; i++)
            {
                replay[i].Render(canvas);
            }

            // One upload for the whole replay, not one per command.
            canvas.Commit();
        }

        private void OnCommandPushed(IPaintCommand command)
        {
            Redraw(command.SurfaceId);
        }

        /// <summary>
        /// A re-scan replaces the room's anchors, so surfaces this renderer painted may no longer
        /// exist. Their canvases go with them — paint left hanging where a wall used to be is worse
        /// than paint that disappeared with its wall (architecture §8.5).
        /// </summary>
        private void OnSurfacesChanged(IPaintableSurfaceProvider provider)
        {
            // The room changed, so a wall reported missing may be back and one that was here may be
            // gone. Whatever is still absent is worth saying once more.
            reportedMissing.Clear();

            pruned.Clear();

            foreach (var entry in canvases)
            {
                var canvas = entry.Value;
                if (canvas == null || canvas.Surface == null || !canvas.Surface.IsValid)
                {
                    pruned.Add(entry.Key);
                }
            }

            for (var i = 0; i < pruned.Count; i++)
            {
                var canvas = canvases[pruned[i]];
                if (canvas != null)
                {
                    Destroy(canvas.gameObject);
                }

                canvases.Remove(pruned[i]);
            }
        }

        private PaintCanvas GetOrCreateCanvas(string surfaceId)
        {
            if (canvases.TryGetValue(surfaceId, out var existing))
            {
                if (existing != null && existing.Surface != null && existing.Surface.IsValid)
                {
                    return existing;
                }

                // Stale entry — the wall went away between renders. Drop it and try to find the
                // surface again, in case the same anchor came back under the same id.
                if (existing != null)
                {
                    Destroy(existing.gameObject);
                }

                canvases.Remove(surfaceId);
            }

            var surface = FindSurface(surfaceId);
            if (surface == null)
            {
                // Not an error: paint whose wall is not in the room right now is skipped cleanly,
                // which is the behaviour M8 needs when a saved anchor does not come back
                // (ADR 0006, architecture §8.5). Said once per surface until the room changes.
                if (reportedMissing.Add(surfaceId))
                {
                    Debug.LogWarning(
                        $"[{nameof(PaintRenderer)}] No surface with id '{surfaceId}' is available, so " +
                        "its paint is not drawn. It will appear if that anchor comes back.", this);
                }

                return null;
            }

            return CreateCanvas(surfaceId, surface);
        }

        private PaintCanvas CreateCanvas(string surfaceId, IPaintableSurface surface)
        {
            // Left unparented on purpose. A canvas positions itself in world space from its anchor's
            // pose and scales itself to the wall's metres; parenting it under this renderer would
            // fold that renderer's own scale into both (architecture §7).
            var canvas = Instantiate(canvasPrefab);
            canvas.name = $"PaintCanvas {surfaceId}";

            if (!canvas.Bind(surface, qualityBudget.Active))
            {
                // Bind already reported why.
                Destroy(canvas.gameObject);
                return null;
            }

            canvases[surfaceId] = canvas;
            reportedMissing.Remove(surfaceId);
            return canvas;
        }

        private IPaintableSurface FindSurface(string surfaceId)
        {
            for (var p = 0; p < providers.Count; p++)
            {
                var surfaces = providers[p].Surfaces;
                for (var s = 0; s < surfaces.Count; s++)
                {
                    var surface = surfaces[s];
                    if (surface != null && surface.IsValid && surface.Id == surfaceId)
                    {
                        return surface;
                    }
                }
            }

            return null;
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
                    $"[{nameof(PaintRenderer)}] No {nameof(IPaintableSurfaceProvider)} assigned, so no " +
                    "command's surface id can be resolved and nothing will ever be drawn. Assign the " +
                    "wall and/or manual-plane provider in the inspector.", this);
            }
        }
    }
}
