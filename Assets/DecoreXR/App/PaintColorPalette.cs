using System;
using System.Collections.Generic;
using UnityEngine;

namespace DecoreXR.App
{
    /// <summary>
    /// The colours the wrist palette offers (ADR 0009). One asset, so adding or changing a colour is
    /// editing data rather than editing a scene or a script.
    /// </summary>
    /// <remarks>
    /// In <c>App</c> and not in <c>Painting</c>, because it is not part of how paint is represented.
    /// A command already carries the only colour model the paint model needs — a
    /// <see cref="Color32"/> (ADR 0003) — and it neither knows nor cares whether that colour came
    /// from a swatch, a save file, or somewhere M8 has not invented yet. What this asset holds is the
    /// menu, which is a question about the UI.
    /// <para>
    /// A <c>ScriptableObject</c> rather than fields on the palette panel, for the same reason the
    /// quality budget is one (architecture §8.3): the set of colours is content, it wants to be
    /// editable without opening the scene, and it should be one list rather than one per place that
    /// shows it.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "PaintColorPalette", menuName = "DecoreXR/Paint Color Palette")]
    public sealed class PaintColorPalette : ScriptableObject
    {
        /// <summary>One colour the user can choose.</summary>
        [Serializable]
        public struct Swatch
        {
            [Tooltip("What this colour is called. Shown to the user where there is room for a name, " +
                     "and what identifies the swatch in the inspector.")]
            public string name;

            [Tooltip("The colour painted when this swatch is chosen.")]
            // No alpha field: paint is opaque. A half-transparent swatch would be indistinguishable
            // from a wall that was not quite painted, and alpha 0 already means "nothing painted
            // here" on a canvas.
            [ColorUsage(showAlpha: false)]
            public Color color;
        }

        [Header("Colours")]
        [Tooltip("The swatches the palette shows, in the order it shows them. The first is what the " +
                 "app starts on.")]
        [SerializeField]
        private Swatch[] swatches =
        {
            new Swatch { name = "Chalk", color = new Color(0.94f, 0.93f, 0.89f) },
            new Swatch { name = "Clay", color = new Color(0.78f, 0.53f, 0.42f) },
            new Swatch { name = "Sage", color = new Color(0.55f, 0.65f, 0.53f) },
            new Swatch { name = "Denim", color = new Color(0.35f, 0.52f, 0.78f) },
            new Swatch { name = "Ochre", color = new Color(0.85f, 0.66f, 0.28f) },
            new Swatch { name = "Ink", color = new Color(0.16f, 0.18f, 0.24f) },
        };

        /// <summary>The swatches, in the order the palette shows them.</summary>
        public IReadOnlyList<Swatch> Swatches => swatches;

        /// <summary>
        /// The colour the app starts on — the first swatch. False when the asset is empty, which is
        /// a palette with nothing to choose from and is the caller's problem to report
        /// (architecture §8.5).
        /// </summary>
        public bool TryGetDefault(out Color color)
        {
            if (swatches.Length == 0)
            {
                color = default;
                return false;
            }

            color = swatches[0].color;
            return true;
        }

        /// <summary>Whether a colour is one this palette offers, compared opaque.</summary>
        /// <remarks>
        /// So that something holding a colour can ask whether the user could ever choose it again — a
        /// colour off the palette is one the UI can never show as current.
        /// </remarks>
        public bool Contains(Color color)
        {
            color.a = 1f;

            for (var i = 0; i < swatches.Length; i++)
            {
                var swatch = swatches[i].color;
                swatch.a = 1f;

                if (swatch == color)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            // The alpha field is hidden, but a value edited before it was hidden, or pasted in from
            // elsewhere, can still be anything. Paint is opaque.
            for (var i = 0; i < swatches.Length; i++)
            {
                if (!Mathf.Approximately(swatches[i].color.a, 1f))
                {
                    swatches[i].color.a = 1f;
                }
            }
        }
    }
}
