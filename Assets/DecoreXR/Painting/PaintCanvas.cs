using DecoreXR.Core;
using DecoreXR.Spatial;
using UnityEngine;

namespace DecoreXR.Painting
{
    /// <summary>
    /// One paintable surface's canvas: a texture holding what has been painted on it, drawn on a quad
    /// that sits on the wall and follows its spatial anchor. This is the display side of ADR 0003 —
    /// the commands in <see cref="PaintHistory"/> are the truth, and this is a render of them that
    /// can be thrown away and rebuilt.
    /// </summary>
    /// <remarks>
    /// Sized from the surface, not from a constant: the texture is allocated once at
    /// <see cref="Bind"/> from the quality budget's texels-per-metre and cap (ADR 0004), and the quad
    /// is scaled to the surface's metres. Reallocating a texture is the one thing that must not
    /// happen while the user paints (architecture §7), so resolution is fixed for the life of the
    /// binding; a wall that genuinely changed size would be re-bound, not resized in place.
    /// <para>
    /// The quad is placed from the surface's live pose every frame rather than parented once, because
    /// <see cref="IPaintableSurface"/> hands out a pose read through the anchor and a re-localisation
    /// can move it under us. It is never positioned relative to the camera (architecture §7).
    /// </para>
    /// <para>
    /// A component on a prefab rather than something assembled in code, so the material and the
    /// offset stay inspector-visible config; the renderer instantiates one per painted surface.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class PaintCanvas : MonoBehaviour, IPaintCanvas
    {
        /// <summary>
        /// Transparent, i.e. "nothing painted here" — the real wall shows through passthrough. A
        /// canvas starts and is cleared to this.
        /// </summary>
        private static readonly Color32 Unpainted = new Color32(0, 0, 0, 0);

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        [Header("Placement")]
        [Tooltip("How far in front of the wall the paint sits, in metres. ~5mm keeps the quad off " +
                 "the wall plane so the two cannot z-fight, while staying close enough to read as " +
                 "paint rather than a floating panel (ADR 0005). The matching depth bias is M4's.")]
        [Min(0f)]
        [SerializeField] private float surfaceOffset = 0.005f;

        private MeshRenderer meshRenderer;
        private Mesh quad;
        private Material material;
        private Texture2D texture;
        private IPaintableSurface surface;

        /// <summary>The surface this canvas paints, or null while unbound.</summary>
        public IPaintableSurface Surface => surface;

        /// <summary>
        /// The surface id this canvas belongs to, matching <see cref="IPaintCommand.SurfaceId"/>, or
        /// null while unbound.
        /// </summary>
        public string SurfaceId => surface?.Id;

        /// <summary>Texture size in texels, decided at <see cref="Bind"/> from the budget.</summary>
        public Vector2Int Resolution { get; private set; }

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();

            BuildQuad();
            InstanceMaterial();

            // Nothing to show until a surface is bound and something is painted on it.
            meshRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            // Texture, mesh and material instance are all GPU resources this component allocated, so
            // they are all this component's to release (architecture §8.4).
            ReleaseTexture();

            if (quad != null)
            {
                Destroy(quad);
                quad = null;
            }

            if (material != null)
            {
                Destroy(material);
                material = null;
            }

            surface = null;
        }

        /// <summary>
        /// Attaches this canvas to a surface and allocates its texture at the resolution the budget
        /// allows. Replaces any previous binding.
        /// </summary>
        /// <returns>
        /// False when the surface cannot be painted — null, already invalid, or with no area. A
        /// canvas that cannot be sized is left unbound and invisible rather than made at some
        /// invented size (architecture §8.5).
        /// </returns>
        public bool Bind(IPaintableSurface target, QualityBudgetConfig.Budget budget)
        {
            Unbind();

            if (target == null || !target.IsValid)
            {
                Debug.LogError($"[{nameof(PaintCanvas)}] Cannot bind to a missing or invalid surface.", this);
                return false;
            }

            var size = target.Size;
            if (size.x <= 0f || size.y <= 0f)
            {
                Debug.LogError(
                    $"[{nameof(PaintCanvas)}] Surface '{target.Id}' measures {size.x}x{size.y}m and has " +
                    "no area to paint.", this);
                return false;
            }

            var resolution = ResolutionFor(size, budget);
            if (!Allocate(resolution))
            {
                return false;
            }

            surface = target;
            Resolution = resolution;
            Follow();
            return true;
        }

        /// <summary>
        /// Detaches from the current surface and frees its texture. The quad stops drawing; the mesh
        /// and material instance are kept, since this canvas may be bound again.
        /// </summary>
        public void Unbind()
        {
            surface = null;
            Resolution = Vector2Int.zero;
            ReleaseTexture();

            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Resets the canvas to unpainted. The renderer calls this before replaying a surface's
        /// commands, so a re-render reflects the command list rather than layering onto the last one.
        /// </summary>
        public void Clear()
        {
            WriteAll(Unpainted);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Writes the CPU-side texels only. Uploading is <see cref="Commit"/>'s job so that replaying
        /// a surface's whole command list costs one upload rather than one per command.
        /// </remarks>
        public void FillAll(Color32 color)
        {
            WriteAll(color);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Rasterized by measuring each candidate texel's distance from the centre <em>in metres</em>
        /// rather than in texels or in <c>(u,v)</c>. That is the only test that stays a circle on a
        /// wall of any shape and at any resolution the budget picked (ADR 0004): it asks the question
        /// the user's radius actually asked, instead of one about the texture's aspect ratio.
        /// <para>
        /// Only the circle's own bounding box is visited, so the cost is the area painted and not the
        /// size of the wall. As with <see cref="FillAll"/>, the texels are written CPU-side and
        /// uploaded once by <see cref="Commit"/>.
        /// </para>
        /// </remarks>
        public void FillCircle(Vector2 center, float radius, Color32 color)
        {
            // Unlike a fill, a circle needs the wall's metres as well as its texels, so this asks for
            // the surface too rather than only the texture.
            if (texture == null || surface == null)
            {
                Debug.LogError($"[{nameof(PaintCanvas)}] Cannot paint: no canvas is bound.", this);
                return;
            }

            // A circle with no radius is not a degenerate case worth reporting — it is what a size
            // gesture reads before the user has opened it (M5-T4).
            if (radius <= 0f)
            {
                return;
            }

            var size = surface.Size;
            var width = Resolution.x;
            var height = Resolution.y;

            // Half a texel, in metres: the width of the band over which a texel goes from inside the
            // circle to outside it. Without it the edge is a staircase, which on a shape this round
            // is the first thing the eye finds.
            var feather = 0.25f * (size.x / width + size.y / height);

            var reach = radius + feather;
            var xMin = Mathf.Max(0, Mathf.FloorToInt((center.x - reach / size.x) * width));
            var xMax = Mathf.Min(width - 1, Mathf.CeilToInt((center.x + reach / size.x) * width));
            var yMin = Mathf.Max(0, Mathf.FloorToInt((center.y - reach / size.y) * height));
            var yMax = Mathf.Min(height - 1, Mathf.CeilToInt((center.y + reach / size.y) * height));

            // Entirely off this wall. Clipped away, not an error: aiming near an edge is normal, and
            // the wall is what the circle is cut off by.
            if (xMin > xMax || yMin > yMax)
            {
                return;
            }

            var texels = texture.GetRawTextureData<Color32>();
            var inner = radius - feather;

            for (var y = yMin; y <= yMax; y++)
            {
                // Texel centres, hence the half: sampling from a texel's corner would shift the whole
                // circle half a texel up and to the left.
                var dy = ((y + 0.5f) / height - center.y) * size.y;
                var row = y * width;

                for (var x = xMin; x <= xMax; x++)
                {
                    var dx = ((x + 0.5f) / width - center.x) * size.x;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance >= reach)
                    {
                        continue;
                    }

                    var index = row + x;

                    if (distance <= inner)
                    {
                        texels[index] = color;
                        continue;
                    }

                    // The edge ring: how much of this texel the circle covers.
                    var coverage = Mathf.InverseLerp(reach, inner, distance);
                    texels[index] = Blend(texels[index], color, coverage);
                }
            }
        }

        /// <summary>
        /// Lays <paramref name="source"/> over <paramref name="destination"/> at the given coverage —
        /// ordinary source-over compositing on non-premultiplied colours.
        /// </summary>
        /// <remarks>
        /// Needed only for the boundary ring, but it has to be right there: an unpainted texel is
        /// fully transparent, and blending towards it without accounting for its alpha would ring
        /// every circle in a dark halo of whatever colour transparent black happens to be.
        /// </remarks>
        private static Color32 Blend(Color32 destination, Color32 source, float coverage)
        {
            var sourceAlpha = source.a / 255f * Mathf.Clamp01(coverage);
            if (sourceAlpha <= 0f)
            {
                return destination;
            }

            var destinationAlpha = destination.a / 255f;
            var keptAlpha = destinationAlpha * (1f - sourceAlpha);
            var outAlpha = sourceAlpha + keptAlpha;

            if (outAlpha <= 0f)
            {
                return Unpainted;
            }

            return new Color32(
                (byte)Mathf.RoundToInt((source.r * sourceAlpha + destination.r * keptAlpha) / outAlpha),
                (byte)Mathf.RoundToInt((source.g * sourceAlpha + destination.g * keptAlpha) / outAlpha),
                (byte)Mathf.RoundToInt((source.b * sourceAlpha + destination.b * keptAlpha) / outAlpha),
                (byte)Mathf.RoundToInt(outAlpha * 255f));
        }

        /// <summary>
        /// Uploads the texels written since the last commit and makes the quad visible. Called once
        /// at the end of a re-render.
        /// </summary>
        public void Commit()
        {
            if (texture == null)
            {
                Debug.LogError($"[{nameof(PaintCanvas)}] Nothing to commit: no canvas is bound.", this);
                return;
            }

            texture.Apply(false);
            meshRenderer.enabled = true;
        }

        /// <summary>
        /// Texels for a surface of this size: the budget's density, reduced uniformly on both axes if
        /// that would exceed the cap (ADR 0004).
        /// </summary>
        /// <remarks>
        /// One density for both axes, rather than clamping each independently, so texels stay square.
        /// A wall wider than it is tall would otherwise be squashed on its long axis only, and every
        /// shape drawn on it from M5 onward would come out stretched. An oversized wall loses density
        /// evenly instead, which is the trade ADR 0004 names.
        /// </remarks>
        private static Vector2Int ResolutionFor(Vector2 size, QualityBudgetConfig.Budget budget)
        {
            var texelsPerMeter = Mathf.Max(1, budget.texelsPerMeter);
            var cap = Mathf.Max(1, budget.maxTextureSize);

            var density = Mathf.Min(texelsPerMeter, cap / Mathf.Max(size.x, size.y));

            return new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(size.x * density), 1, cap),
                Mathf.Clamp(Mathf.RoundToInt(size.y * density), 1, cap));
        }

        private bool Allocate(Vector2Int resolution)
        {
            if (material == null)
            {
                // Awake already said why. Allocating a texture nothing can display would only add a
                // second, less useful error.
                return false;
            }

            // No mip chain: mips would add a third again to a budget that is shared with passthrough
            // (ADR 0004), and paint is looked at from roughly wall-facing distances where they buy
            // little. Revisit under M4's alignment and occlusion tuning if it aliases at grazing
            // angles.
            texture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, mipChain: false)
            {
                name = $"PaintCanvas {resolution.x}x{resolution.y}",
                // Clamp, so a shape drawn at the very edge of a wall cannot bleed round to the
                // opposite edge.
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            WriteAll(Unpainted);
            texture.Apply(false);

            material.SetTexture(BaseMapId, texture);
            return true;
        }

        private void ReleaseTexture()
        {
            if (texture == null)
            {
                return;
            }

            if (material != null)
            {
                // Drop the material's reference before destroying the texture, so nothing is left
                // pointing at a destroyed object.
                material.SetTexture(BaseMapId, null);
            }

            Destroy(texture);
            texture = null;
        }

        /// <summary>
        /// Writes every texel directly in the texture's own CPU-side buffer. Going through the raw
        /// data rather than <c>SetPixels32</c> avoids keeping a second full-size colour array per
        /// wall, which at the 1024² cap is megabytes a wall (ADR 0004).
        /// </summary>
        private void WriteAll(Color32 color)
        {
            if (texture == null)
            {
                Debug.LogError($"[{nameof(PaintCanvas)}] Cannot paint: no canvas is bound.", this);
                return;
            }

            var texels = texture.GetRawTextureData<Color32>();
            for (var i = 0; i < texels.Length; i++)
            {
                texels[i] = color;
            }
        }

        private void LateUpdate()
        {
            if (surface == null)
            {
                return;
            }

            // A wall can go away under the user when the room is re-scanned. Stop drawing paint at
            // the last place the wall was, which would be paint floating in the room
            // (architecture §8.5).
            if (!surface.IsValid)
            {
                Unbind();
                return;
            }

            Follow();
        }

        /// <summary>
        /// Puts the quad on the surface: centred on its pose, lifted along its normal, scaled to its
        /// metres. LateUpdate, so it lands after anything that moved the anchor this frame.
        /// </summary>
        private void Follow()
        {
            var pose = surface.Pose;

            transform.SetPositionAndRotation(
                pose.position + surface.Normal * surfaceOffset,
                pose.rotation);

            var size = surface.Size;
            transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        /// <summary>
        /// A unit quad in local XY facing +Z, matching <see cref="IPaintableSurface"/>'s frame: +X is
        /// <c>u</c>, +Y is <c>v</c>, +Z is out of the wall. Built here rather than using Unity's
        /// built-in quad because that one faces the other way, and a wall painted on its back face is
        /// a bug that only shows up on device.
        /// </summary>
        private void BuildQuad()
        {
            quad = new Mesh { name = "PaintCanvas Quad" };

            quad.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
            });

            // (u,v) 0→1 bottom-left to top-right, the same space the commands are expressed in
            // (ADR 0003), so a command's coordinates need no flipping to reach a texel.
            quad.SetUVs(0, new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            });

            quad.SetNormals(new[]
            {
                Vector3.forward,
                Vector3.forward,
                Vector3.forward,
                Vector3.forward,
            });

            // Wound so that Unity's front face — cross(b - a, c - a) for a triangle (a, b, c) —
            // comes out along +Z, agreeing with the normals above and with the surface's outward
            // direction. Get this backwards and the paint is only visible from inside the wall.
            quad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);

            quad.RecalculateBounds();
            quad.UploadMeshData(markNoLongerReadable: true);

            GetComponent<MeshFilter>().sharedMesh = quad;
        }

        /// <summary>
        /// Each canvas draws its own texture, so each needs its own material. Instanced from the
        /// shared one in the inspector; <c>sharedMaterial</c> is read, never assigned, so the asset
        /// on disk is left alone.
        /// </summary>
        private void InstanceMaterial()
        {
            var template = meshRenderer.sharedMaterial;
            if (template == null)
            {
                Debug.LogError(
                    $"[{nameof(PaintCanvas)}] No material on the {nameof(MeshRenderer)}, so painted " +
                    "walls would draw in whatever Unity falls back to. Assign the PaintCanvas " +
                    "material on the prefab.", this);
                enabled = false;
                return;
            }

            material = new Material(template) { name = $"{template.name} (canvas instance)" };
            meshRenderer.material = material;
        }
    }
}
