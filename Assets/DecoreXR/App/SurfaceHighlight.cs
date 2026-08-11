using DecoreXR.Interaction;
using DecoreXR.Spatial;
using UnityEngine;

namespace DecoreXR.App
{
    /// <summary>
    /// Draws an outline around the wall the pointer is on, and keeps it there once the wall is
    /// selected. Without it the user has to guess which wall a press will land on.
    /// </summary>
    /// <remarks>
    /// An outline rather than a filled overlay: in passthrough the wall is the thing the user is
    /// looking at, and covering it to say "this one" hides what they are about to paint. The outline
    /// tracks the surface's corners every frame because a surface follows its spatial anchor and can
    /// shift under a re-localisation (architecture §7).
    /// <para>
    /// Presentation only — it reads <see cref="SurfaceSelection"/> and never decides anything about
    /// selection itself, so UI stays in <c>App</c> (ADR 0009, architecture §4).
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class SurfaceHighlight : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("The selection this outline reflects.")]
        [SerializeField] private SurfaceSelection selection;

        [Header("Appearance")]
        [Tooltip("Outline material while a surface is merely hovered.")]
        [SerializeField] private Material hoverMaterial;

        [Tooltip("Outline material once the surface is the selected one.")]
        [SerializeField] private Material selectedMaterial;

        [Tooltip("How far in front of the surface the outline sits, in metres. Enough to clear the " +
                 "wall without looking detached from it — the paint canvas gets the same treatment " +
                 "for the same reason (ADR 0005).")]
        [Min(0f)]
        [SerializeField] private float surfaceOffset = 0.005f;

        [Tooltip("Outline thickness in metres.")]
        [Min(0.001f)]
        [SerializeField] private float lineWidth = 0.01f;

        private static readonly Vector2[] Corners =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };

        private LineRenderer outline;

        private void Awake()
        {
            outline = GetComponent<LineRenderer>();

            outline.useWorldSpace = true;
            outline.loop = true;
            outline.positionCount = Corners.Length;
            outline.alignment = LineAlignment.View;
            outline.textureMode = LineTextureMode.Stretch;

            // A hint, not a light: it should not cast, receive, or be probe-lit.
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            outline.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        private void Reset()
        {
            selection = FindAnyObjectByType<SurfaceSelection>();
        }

        private void OnEnable()
        {
            if (selection == null)
            {
                Debug.LogError(
                    $"[{nameof(SurfaceHighlight)}] No {nameof(SurfaceSelection)} assigned, so there is " +
                    "nothing to outline. Assign one in the inspector.", this);
                enabled = false;
                return;
            }

            if (hoverMaterial == null || selectedMaterial == null)
            {
                // Left unassigned the outline would still draw, in whatever the LineRenderer happens
                // to hold — a wrong-looking hint is harder to diagnose than a missing one.
                Debug.LogError(
                    $"[{nameof(SurfaceHighlight)}] Both the hover and selected outline materials must " +
                    "be assigned. Assign them in the inspector.", this);
                enabled = false;
                return;
            }

            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void LateUpdate()
        {
            // The hovered surface is what a press would take, so it wins the outline. Falling back to
            // the selected one keeps the user's choice visible when they look away from it.
            var surface = selection.HasHover ? selection.Hover.Surface : selection.Selected;
            if (surface == null || !surface.IsValid)
            {
                Hide();
                return;
            }

            var isSelected = ReferenceEquals(surface, selection.Selected);
            var material = isSelected ? selectedMaterial : hoverMaterial;
            if (material != null && outline.sharedMaterial != material)
            {
                // sharedMaterial, not material: assigning `material` would instance a copy per frame
                // and leak it (architecture §8.4).
                outline.sharedMaterial = material;
            }

            outline.widthMultiplier = lineWidth;
            Trace(surface);
            outline.enabled = true;
        }

        /// <summary>
        /// Walks the surface's four corners in its own <c>(u,v)</c> space, so a wall and a manual
        /// plane are outlined by identical code (ADR 0010).
        /// </summary>
        private void Trace(IPaintableSurface surface)
        {
            var lift = surface.Normal * surfaceOffset;

            for (var i = 0; i < Corners.Length; i++)
            {
                outline.SetPosition(i, surface.GetWorldPoint(Corners[i]) + lift);
            }
        }

        private void Hide()
        {
            if (outline != null)
            {
                outline.enabled = false;
            }
        }
    }
}
