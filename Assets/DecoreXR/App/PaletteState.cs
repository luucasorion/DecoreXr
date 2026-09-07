using System;
using UnityEngine;

namespace DecoreXR.App
{
    /// <summary>
    /// What the user has chosen to paint with: one tool and one colour, held in one place so every
    /// tool in <c>App</c> reads the same answer (ADR 0009).
    /// </summary>
    /// <remarks>
    /// Split out from the palette UI rather than living on it, because the two have different
    /// lifetimes and different jobs. The palette is a panel that comes and goes with the user's wrist
    /// (<see cref="WristPalette"/>); the choice it last made has to outlive it, and the tools that
    /// act on that choice must not have to reach into a UI object to find it. Buttons write here,
    /// tools read here, and neither knows the other exists.
    /// <para>
    /// A <c>MonoBehaviour</c> wired in the inspector, not a static: the same reason
    /// <c>PaintHistory</c> is one — architecture §4 rules out singletons reachable from anywhere.
    /// </para>
    /// <para>
    /// The scaffold M5-T1 needs, and no more. The list of colours a user can pick from is a config
    /// asset in M5-T3; this only remembers which one is current.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PaletteState : MonoBehaviour
    {
        [Header("Current choice")]
        [Tooltip("The tool the next press applies. Also the tool the palette starts on.")]
        [SerializeField] private PaintTool activeTool = PaintTool.Fill;

        [Tooltip("The colour the next paint command uses. Also the colour the palette starts on. " +
                 "M5-T3 replaces picking this in the inspector with picking it from the palette's " +
                 "colour config.")]
        [SerializeField] private Color activeColor = new Color(0.35f, 0.52f, 0.78f, 1f);

        /// <summary>The tool the next paint action applies.</summary>
        public PaintTool ActiveTool => activeTool;

        /// <summary>The colour the next paint action uses.</summary>
        public Color ActiveColor => activeColor;

        /// <summary>Raised when <see cref="ActiveTool"/> changes, carrying the new tool.</summary>
        public event Action<PaintTool> ToolChanged;

        /// <summary>Raised when <see cref="ActiveColor"/> changes, carrying the new colour.</summary>
        public event Action<Color> ColorChanged;

        /// <summary>
        /// Chooses a tool. Does nothing if it is already the active one, so a button that re-asserts
        /// the current tool does not make listeners think the user changed their mind.
        /// </summary>
        public void SelectTool(PaintTool tool)
        {
            if (activeTool == tool)
            {
                return;
            }

            activeTool = tool;
            ToolChanged?.Invoke(activeTool);
        }

        /// <summary>
        /// Chooses a colour. As with <see cref="SelectTool"/>, re-choosing the current colour is not
        /// a change.
        /// </summary>
        /// <remarks>
        /// The alpha is forced opaque. Paint that is partly transparent would read as a wall that was
        /// not quite painted, and "nothing painted here" is already what alpha 0 means on a canvas
        /// (see <c>PaintCanvas</c>) — letting a swatch mean something in between would make the two
        /// indistinguishable.
        /// </remarks>
        public void SelectColor(Color color)
        {
            color.a = 1f;

            if (activeColor == color)
            {
                return;
            }

            activeColor = color;
            ColorChanged?.Invoke(activeColor);
        }

        private void OnDestroy()
        {
            // Nothing should be holding a torn-down palette state, and a stale subscriber acting on a
            // choice that is going away is worse than no notification (architecture §8.4).
            ToolChanged = null;
            ColorChanged = null;
        }
    }
}
