using System.Collections.Generic;
using UnityEngine;

namespace DecoreXR.App
{
    /// <summary>
    /// Builds the palette's colour swatches from <see cref="PaintColorPalette"/> — one
    /// <see cref="PaletteColorButton"/> per colour in the asset (ADR 0009).
    /// </summary>
    /// <remarks>
    /// Spawned rather than hand-placed so that the asset is genuinely the list: add a colour and the
    /// palette grows, reorder it and the palette reorders, with no scene to keep in step and no
    /// swatch that shows one colour while painting another. Layout is left to a uGUI layout group on
    /// the container, so how the swatches are arranged stays inspector config and none of it is
    /// computed here.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PaletteColorRow : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The palette state the swatches write to.")]
        [SerializeField] private PaletteState paletteState;

        [Tooltip("The colours to offer.")]
        [SerializeField] private PaintColorPalette palette;

        [Header("UI")]
        [Tooltip("The swatch prefab instantiated once per colour.")]
        [SerializeField] private PaletteColorButton swatchPrefab;

        [Tooltip("Where the swatches are parented. Put the layout group here.")]
        [SerializeField] private RectTransform container;

        private readonly List<PaletteColorButton> swatches = new List<PaletteColorButton>();

        /// <summary>The swatches currently built, in palette order.</summary>
        public IReadOnlyList<PaletteColorButton> Swatches => swatches;

        private void Reset()
        {
            paletteState = FindAnyObjectByType<PaletteState>();
        }

        private void OnEnable()
        {
            if (paletteState == null || palette == null || swatchPrefab == null || container == null)
            {
                Debug.LogError(
                    $"[{nameof(PaletteColorRow)}] Needs the {nameof(PaletteState)}, a " +
                    $"{nameof(PaintColorPalette)}, a swatch prefab and a container; without all four " +
                    "the palette would offer no colours at all. Assign them in the inspector.", this);
                enabled = false;
                return;
            }

            Build();
        }

        private void OnDisable()
        {
            Clear();
        }

        /// <summary>
        /// Rebuilds the swatches from the asset. Public so an editor that changes the palette can ask
        /// for the row to catch up, rather than the row polling an asset that almost never changes.
        /// </summary>
        public void Build()
        {
            Clear();

            var colors = palette.Swatches;
            if (colors.Count == 0)
            {
                Debug.LogError(
                    $"[{nameof(PaletteColorRow)}] '{palette.name}' lists no colours, so the palette " +
                    "has nothing to choose from. Add at least one swatch to the asset.", this);
                return;
            }

            for (var i = 0; i < colors.Count; i++)
            {
                var swatch = Instantiate(swatchPrefab, container);
                swatch.name = string.IsNullOrEmpty(colors[i].name)
                    ? $"Swatch {i}"
                    : $"Swatch {colors[i].name}";

                swatch.Bind(paletteState, colors[i].color);
                swatches.Add(swatch);
            }

            // A colour that is not on the palette can never be shown as current or chosen again, so
            // starting the state on one would leave every swatch unmarked and the user unable to get
            // back to what they are apparently painting with. Only then is the choice overridden —
            // a colour that IS on the palette is left alone, so a restored session keeps its colour
            // (ADR 0006, M8).
            if (!palette.Contains(paletteState.ActiveColor) && palette.TryGetDefault(out var start))
            {
                paletteState.SelectColor(start);
            }
        }

        private void Clear()
        {
            for (var i = 0; i < swatches.Count; i++)
            {
                if (swatches[i] == null)
                {
                    continue;
                }

                // Out of the container before being destroyed, because Destroy only takes effect at
                // the end of the frame: leaving them parented would give the layout group a frame in
                // which it sees the old swatches and the new ones together and lays out both.
                swatches[i].transform.SetParent(null, false);
                Destroy(swatches[i].gameObject);
            }

            swatches.Clear();
        }
    }
}
