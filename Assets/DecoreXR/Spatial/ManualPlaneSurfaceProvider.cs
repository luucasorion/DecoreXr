using System;
using System.Collections.Generic;
using UnityEngine;

namespace DecoreXR.Spatial
{
    /// <summary>
    /// Places and publishes hand-dropped planes as <see cref="IPaintableSurface"/>s — the fallback
    /// for a room that was never scanned, or a headset where scene permission is refused
    /// (ADR 0010). Painting consumes these exactly as it does MRUK walls.
    /// </summary>
    /// <remarks>
    /// This owns placement and anchoring only. Aiming the plane is the pointer's job and arrives
    /// with <c>IPointerSource</c> in M2 (ADR 0002); until then callers supply the pose directly.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ManualPlaneSurfaceProvider : MonoBehaviour, IPaintableSurfaceProvider
    {
        private static readonly IReadOnlyList<IPaintableSurface> Empty = Array.Empty<IPaintableSurface>();

        [Tooltip("Default size in metres of a dropped plane, as width by height. A fallback plane " +
                 "is a stand-in for a wall, so this defaults to roughly wall-sized.")]
        [SerializeField] private Vector2 defaultSize = new Vector2(2f, 2.5f);

        [Tooltip("Optional visual shown at each placed plane, e.g. a thin quad outlining it. " +
                 "Instantiated as a child of the plane's anchor; leave empty for an invisible plane.")]
        [SerializeField] private GameObject planeVisualPrefab;

        /// <summary>One placed plane and the GameObject that carries its anchor.</summary>
        private readonly struct Placement
        {
            public Placement(IPaintableSurface surface, GameObject host)
            {
                Surface = surface;
                Host = host;
            }

            public IPaintableSurface Surface { get; }
            public GameObject Host { get; }
        }

        private readonly List<Placement> placements = new List<Placement>();

        // Published separately from `placements` so callers can hold the list across a frame while
        // a plane is placed or removed — the seam promises replacement, not mutation.
        private IReadOnlyList<IPaintableSurface> surfaces = Empty;

        /// <inheritdoc />
        public event Action<IPaintableSurfaceProvider> SurfacesChanged;

        /// <inheritdoc />
        public IReadOnlyList<IPaintableSurface> Surfaces => surfaces;

        /// <summary>The size a plane gets when <see cref="Place(Pose)"/> is used.</summary>
        public Vector2 DefaultSize => defaultSize;

        private void OnDestroy()
        {
            // The planes belong to this provider; take them down with it so nothing is left
            // floating in the room (architecture §8.4).
            foreach (var placement in placements)
            {
                if (placement.Host != null)
                {
                    Destroy(placement.Host);
                }
            }

            placements.Clear();
            surfaces = Empty;
            SurfacesChanged = null;
        }

        /// <summary>Drops a plane of the default size at <paramref name="pose"/>.</summary>
        public IPaintableSurface Place(Pose pose) => Place(pose, defaultSize);

        /// <summary>
        /// Drops a plane at <paramref name="pose"/> — origin at the plane's centre, +Z facing the
        /// user — and starts anchoring it. The surface is returned and published immediately; the
        /// spatial anchor lands a moment later and gives it a persistable <see cref="IPaintableSurface.Id"/>.
        /// </summary>
        public IPaintableSurface Place(Pose pose, Vector2 size)
        {
            if (size.x <= 0f || size.y <= 0f)
            {
                Debug.LogError(
                    $"[{nameof(ManualPlaneSurfaceProvider)}] Refusing to place a plane of size {size}; " +
                    "width and height must both be positive.", this);
                return null;
            }

            var host = new GameObject($"Manual Plane {placements.Count + 1}");
            host.transform.SetPositionAndRotation(pose.position, pose.rotation);

            if (planeVisualPrefab != null)
            {
                var visual = Instantiate(planeVisualPrefab, host.transform);
                visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            }

            // Anchoring is what keeps the plane on the real surface instead of drifting with the
            // headset, and is what makes its paint persistable (architecture §7, ADR 0006).
            var anchor = host.AddComponent<OVRSpatialAnchor>();

            var surface = new ManualPlaneSurface(host.transform, anchor, size);
            placements.Add(new Placement(surface, host));
            Publish();
            return surface;
        }

        /// <summary>
        /// Removes a previously placed plane. Unknown surfaces are ignored rather than treated as
        /// an error — the caller may be clearing something already gone.
        /// </summary>
        public bool Remove(IPaintableSurface surface)
        {
            var index = placements.FindIndex(p => ReferenceEquals(p.Surface, surface));
            if (index < 0)
            {
                return false;
            }

            var host = placements[index].Host;
            placements.RemoveAt(index);

            if (host != null)
            {
                Destroy(host);
            }

            Publish();
            return true;
        }

        /// <summary>Removes every placed plane.</summary>
        public void Clear()
        {
            if (placements.Count == 0)
            {
                return;
            }

            foreach (var placement in placements)
            {
                if (placement.Host != null)
                {
                    Destroy(placement.Host);
                }
            }

            placements.Clear();
            Publish();
        }

        /// <summary>
        /// Swaps in a fresh snapshot of the current planes and announces it. Replacing rather than
        /// mutating is what lets a caller hold <see cref="Surfaces"/> across a frame safely.
        /// </summary>
        private void Publish()
        {
            if (placements.Count == 0)
            {
                surfaces = Empty;
            }
            else
            {
                var snapshot = new IPaintableSurface[placements.Count];
                for (var i = 0; i < placements.Count; i++)
                {
                    snapshot[i] = placements[i].Surface;
                }

                surfaces = snapshot;
            }

            SurfacesChanged?.Invoke(this);
        }
    }
}
