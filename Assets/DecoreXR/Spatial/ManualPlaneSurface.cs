using System;
using UnityEngine;

namespace DecoreXR.Spatial
{
    /// <summary>
    /// A flat plane the user dropped by hand, standing in for an MRUK wall when the room was
    /// never scanned (ADR 0010). It implements the same <see cref="IPaintableSurface"/> seam, so
    /// painting cannot tell it apart from a real wall.
    /// </summary>
    /// <remarks>
    /// The plane is backed by an <see cref="OVRSpatialAnchor"/> so it holds its place in the room
    /// rather than drifting with the headset (architecture §7), and so its UUID can key persisted
    /// paint the same way a wall anchor's does (ADR 0006). The anchor is created asynchronously:
    /// until it exists the surface still works, it just has no persistable identity yet.
    /// </remarks>
    public sealed class ManualPlaneSurface : IPaintableSurface
    {
        private readonly Transform anchorTransform;
        private readonly OVRSpatialAnchor anchor;
        private readonly Vector2 size;

        internal ManualPlaneSurface(Transform anchorTransform, OVRSpatialAnchor anchor, Vector2 size)
        {
            this.anchorTransform = anchorTransform;
            this.anchor = anchor;
            this.size = size;
        }

        /// <summary>
        /// The spatial anchor UUID once the anchor has been created, otherwise empty. Paint on a
        /// plane whose anchor never materialised simply is not persisted — a clean fail rather
        /// than a key that would not survive a restart (ADR 0006, architecture §8.5).
        /// </summary>
        public string Id
        {
            get
            {
                if (anchor == null || !anchor.Created)
                {
                    return string.Empty;
                }

                var uuid = anchor.Uuid;
                return uuid == Guid.Empty ? string.Empty : uuid.ToString();
            }
        }

        /// <summary>True while the plane's GameObject is alive.</summary>
        public bool IsValid => anchorTransform != null;

        /// <summary>True once the backing spatial anchor exists, so <see cref="Id"/> is usable.</summary>
        public bool IsAnchored => anchor != null && anchor.Created;

        /// <inheritdoc />
        public Pose Pose => new Pose(anchorTransform.position, anchorTransform.rotation);

        /// <inheritdoc />
        public Vector2 Size => size;

        /// <inheritdoc />
        public Vector3 Normal => anchorTransform.forward;

        /// <inheritdoc />
        public bool TryGetUv(Vector3 worldPoint, out Vector2 uv)
        {
            var local = anchorTransform.InverseTransformPoint(worldPoint);
            uv = new Vector2(local.x / size.x + 0.5f, local.y / size.y + 0.5f);

            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        /// <inheritdoc />
        public Vector3 GetWorldPoint(Vector2 uv)
        {
            var local = new Vector3((uv.x - 0.5f) * size.x, (uv.y - 0.5f) * size.y, 0f);
            return anchorTransform.TransformPoint(local);
        }
    }
}
