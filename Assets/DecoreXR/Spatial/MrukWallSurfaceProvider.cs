using System;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace DecoreXR.Spatial
{
    /// <summary>
    /// Publishes the current room's MRUK walls as <see cref="IPaintableSurface"/>s once
    /// <see cref="SceneLoader"/> reports a loaded scene (ADR 0001, ADR 0010).
    /// </summary>
    /// <remarks>
    /// The surface list is rebuilt rather than patched whenever the room changes, so a re-scan
    /// cannot leave stale anchors behind — a wall that disappeared simply stops being offered
    /// (architecture §8.5). Requires a <see cref="SceneLoader"/> on the same GameObject.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SceneLoader))]
    public sealed class MrukWallSurfaceProvider : MonoBehaviour, IPaintableSurfaceProvider
    {
        private static readonly IReadOnlyList<IPaintableSurface> Empty = Array.Empty<IPaintableSurface>();

        private SceneLoader sceneLoader;
        private IReadOnlyList<IPaintableSurface> surfaces = Empty;

        /// <inheritdoc />
        public event Action<IPaintableSurfaceProvider> SurfacesChanged;

        /// <inheritdoc />
        public IReadOnlyList<IPaintableSurface> Surfaces => surfaces;

        private void Awake()
        {
            sceneLoader = GetComponent<SceneLoader>();
        }

        private void OnEnable()
        {
            sceneLoader.StatusChanged += OnSceneLoadStatusChanged;

            // The load may already have finished before this component was enabled.
            if (sceneLoader.HasScene)
            {
                Rebuild();
            }
        }

        private void OnDisable()
        {
            sceneLoader.StatusChanged -= OnSceneLoadStatusChanged;
            UnsubscribeFromRoomEvents();
        }

        private void OnDestroy()
        {
            SurfacesChanged = null;
        }

        private void OnSceneLoadStatusChanged(SceneLoadStatus status)
        {
            if (status == SceneLoadStatus.Ready)
            {
                SubscribeToRoomEvents();
                Rebuild();
            }
            else if (status.NeedsFallback())
            {
                // No scene means no walls. Publish that plainly so App can offer Space Setup or
                // the manual plane (M1-T3, M1-T4) instead of leaving stale surfaces on offer.
                Publish(Empty);
            }
        }

        /// <summary>
        /// Re-reads the current room's walls. Safe to call at any time; publishes an empty list
        /// when there is no current room.
        /// </summary>
        public void Rebuild()
        {
            var mruk = MRUK.Instance;
            var room = mruk != null ? mruk.GetCurrentRoom() : null;
            if (room == null)
            {
                Publish(Empty);
                return;
            }

            var walls = room.WallAnchors;
            var built = new List<IPaintableSurface>(walls.Count);
            foreach (var wall in walls)
            {
                var surface = MrukWallSurface.TryCreate(wall);
                if (surface != null)
                {
                    built.Add(surface);
                }
            }

            if (built.Count == 0)
            {
                Debug.LogWarning(
                    $"[{nameof(MrukWallSurfaceProvider)}] The room loaded but exposes no paintable " +
                    "wall faces. The user may need to re-run Space Setup.", this);
            }

            Publish(built);
        }

        private void Publish(IReadOnlyList<IPaintableSurface> next)
        {
            if (surfaces.Count == 0 && next.Count == 0)
            {
                return;
            }

            surfaces = next;
            SurfacesChanged?.Invoke(this);
        }

        private void SubscribeToRoomEvents()
        {
            var mruk = MRUK.Instance;
            if (mruk == null)
            {
                return;
            }

            // Idempotent: UnityEvent.AddListener would double up, so drop any prior hook first.
            UnsubscribeFromRoomEvents();
            mruk.RoomCreatedEvent.AddListener(OnRoomChanged);
            mruk.RoomUpdatedEvent.AddListener(OnRoomChanged);
            mruk.RoomRemovedEvent.AddListener(OnRoomChanged);
        }

        private void UnsubscribeFromRoomEvents()
        {
            var mruk = MRUK.Instance;
            if (mruk == null)
            {
                return;
            }

            mruk.RoomCreatedEvent.RemoveListener(OnRoomChanged);
            mruk.RoomUpdatedEvent.RemoveListener(OnRoomChanged);
            mruk.RoomRemovedEvent.RemoveListener(OnRoomChanged);
        }

        private void OnRoomChanged(MRUKRoom room) => Rebuild();
    }
}
