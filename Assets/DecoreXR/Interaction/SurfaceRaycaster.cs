using System.Collections.Generic;
using DecoreXR.Spatial;
using UnityEngine;

namespace DecoreXR.Interaction
{
    /// <summary>
    /// Turns a pointer ray into the surface it is aimed at and where on that surface
    /// (architecture §6 step 4).
    /// </summary>
    /// <remarks>
    /// The intersection is computed against <see cref="IPaintableSurface"/>'s own plane and
    /// rectangle rather than through MRUK's raycasts or Unity colliders. That is what lets an MRUK
    /// wall and a hand-placed fallback plane be hit by the same code, and it keeps MRUK types out of
    /// this assembly (ADR 0010, architecture §8.2). Surfaces have no colliders to hit anyway — they
    /// are anchors with dimensions, not geometry.
    /// <para>
    /// Only the front of a surface counts. A wall's normal faces into the room, so a ray travelling
    /// with the normal is coming from behind it, and painting the far side of a wall the user cannot
    /// see would be a bug rather than a feature.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SurfaceRaycaster : MonoBehaviour
    {
        [Tooltip("Where paintable surfaces come from — the MRUK wall provider, the manual-plane " +
                 "provider, or both. Each entry must implement IPaintableSurfaceProvider.")]
        [SerializeField] private Component[] surfaceProviders = new Component[0];

        [Tooltip("How far the pointer reaches, in metres. Beyond this the user is aiming at nothing, " +
                 "which is better than letting a slight tilt land paint on a wall across the room.")]
        [Min(0.1f)]
        [SerializeField] private float maxDistance = 10f;

        private readonly List<IPaintableSurfaceProvider> providers = new List<IPaintableSurfaceProvider>();
        private bool providersResolved;

        private void OnEnable()
        {
            ResolveProviders();
        }

        private void OnDisable()
        {
            // Re-enabling re-reads the inspector list, so a provider swapped out while disabled is
            // picked up rather than remembered.
            providers.Clear();
            providersResolved = false;
        }

        private void OnValidate()
        {
            for (var i = 0; i < surfaceProviders.Length; i++)
            {
                var candidate = surfaceProviders[i];
                if (candidate != null && !(candidate is IPaintableSurfaceProvider))
                {
                    Debug.LogWarning(
                        $"[{nameof(SurfaceRaycaster)}] {candidate.GetType().Name} in slot {i} is not " +
                        "an IPaintableSurfaceProvider and will be ignored.", this);
                }
            }
        }

        /// <summary>
        /// Finds the nearest paintable surface along <paramref name="ray"/>. Returns false when the
        /// user is aiming at nothing — empty room, past the reach limit, or off the edge of every
        /// surface — which is a normal state to be in, not an error.
        /// </summary>
        public bool TryRaycast(Ray ray, out SurfaceHit hit)
        {
            hit = default;

            if (!providersResolved)
            {
                ResolveProviders();
            }

            var nearest = float.PositiveInfinity;
            var found = false;

            // Indexed loops throughout: this runs every frame while the user aims, and a foreach
            // over IReadOnlyList would allocate an enumerator each time (architecture §7).
            for (var p = 0; p < providers.Count; p++)
            {
                var surfaces = providers[p].Surfaces;
                for (var s = 0; s < surfaces.Count; s++)
                {
                    var surface = surfaces[s];
                    if (surface == null || !surface.IsValid)
                    {
                        continue;
                    }

                    if (!TryIntersect(ray, surface, out var distance, out var point, out var uv))
                    {
                        continue;
                    }

                    if (distance >= nearest)
                    {
                        continue;
                    }

                    nearest = distance;
                    hit = new SurfaceHit(surface, uv, point, distance);
                    found = true;
                }
            }

            return found;
        }

        private bool TryIntersect(
            Ray ray, IPaintableSurface surface, out float distance, out Vector3 point, out Vector2 uv)
        {
            distance = 0f;
            point = default;
            uv = default;

            var normal = surface.Normal;
            var alignment = Vector3.Dot(normal, ray.direction);

            // Zero means the ray runs along the surface and never lands on it; positive means the
            // ray agrees with the outward normal, i.e. it is approaching from behind.
            if (alignment >= -Mathf.Epsilon)
            {
                return false;
            }

            distance = Vector3.Dot(surface.Pose.position - ray.origin, normal) / alignment;
            if (distance < 0f || distance > maxDistance)
            {
                return false;
            }

            point = ray.origin + ray.direction * distance;

            // The plane is infinite; the paintable rectangle is not. TryGetUv is what draws the edge.
            return surface.TryGetUv(point, out uv);
        }

        private void ResolveProviders()
        {
            providers.Clear();
            providersResolved = true;

            for (var i = 0; i < surfaceProviders.Length; i++)
            {
                if (surfaceProviders[i] is IPaintableSurfaceProvider provider)
                {
                    providers.Add(provider);
                }
            }

            if (providers.Count == 0)
            {
                Debug.LogError(
                    $"[{nameof(SurfaceRaycaster)}] No surface providers assigned, so nothing can ever " +
                    "be aimed at. Assign the wall and/or manual-plane provider in the inspector.", this);
            }
        }
    }
}
