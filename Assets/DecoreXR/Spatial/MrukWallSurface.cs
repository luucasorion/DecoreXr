using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace DecoreXR.Spatial
{
    /// <summary>
    /// Adapts one MRUK <c>WALL_FACE</c> anchor to <see cref="IPaintableSurface"/> (ADR 0001,
    /// ADR 0010). This is the only place a wall's MRUK-ness is visible; everything downstream
    /// sees a pose, a size, and a <c>(u,v)</c> mapping (architecture §8.2).
    /// </summary>
    /// <remarks>
    /// MRUK gives a plane anchor whose rectangle lies in the anchor's local XY at z = 0, with
    /// <c>transform.forward</c> as the room-facing normal. <see cref="MRUKAnchor.PlaneRect"/> is
    /// not necessarily centred on the anchor origin, so the rect's own centre is carried as a
    /// local offset rather than assumed to be zero.
    /// </remarks>
    public sealed class MrukWallSurface : IPaintableSurface
    {
        private readonly MRUKAnchor anchor;
        private readonly Vector2 localCentre;
        private readonly Vector2 size;
        private readonly string id;

        private MrukWallSurface(MRUKAnchor anchor, Rect planeRect)
        {
            this.anchor = anchor;
            localCentre = planeRect.center;
            size = planeRect.size;
            id = anchor.Anchor.Uuid.ToString();
        }

        /// <summary>
        /// Wraps a wall anchor, or returns null when it carries no plane to paint on — a
        /// malformed anchor is skipped, not faked (architecture §8.5).
        /// </summary>
        public static MrukWallSurface TryCreate(MRUKAnchor anchor)
        {
            if (anchor == null || !anchor.PlaneRect.HasValue)
            {
                return null;
            }

            var planeRect = anchor.PlaneRect.Value;
            if (planeRect.width <= 0f || planeRect.height <= 0f)
            {
                return null;
            }

            return new MrukWallSurface(anchor, planeRect);
        }

        /// <summary>The wall's spatial anchor UUID — the persistence key (ADR 0006).</summary>
        public string Id => id;

        /// <inheritdoc />
        public bool IsValid => anchor != null;

        /// <inheritdoc />
        public Pose Pose
        {
            get
            {
                // Read through the anchor's transform every time so the surface follows the
                // spatial anchor rather than a snapshot taken at load (architecture §7).
                var t = anchor.transform;
                return new Pose(t.TransformPoint(localCentre), t.rotation);
            }
        }

        /// <inheritdoc />
        public Vector2 Size => size;

        /// <inheritdoc />
        public Vector3 Normal => anchor.transform.forward;

        /// <inheritdoc />
        public bool TryGetUv(Vector3 worldPoint, out Vector2 uv)
        {
            var local = anchor.transform.InverseTransformPoint(worldPoint);
            uv = new Vector2(
                (local.x - localCentre.x) / size.x + 0.5f,
                (local.y - localCentre.y) / size.y + 0.5f);

            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        /// <inheritdoc />
        public Vector3 GetWorldPoint(Vector2 uv)
        {
            var local = new Vector3(
                localCentre.x + (uv.x - 0.5f) * size.x,
                localCentre.y + (uv.y - 0.5f) * size.y,
                0f);

            return anchor.transform.TransformPoint(local);
        }
    }
}
