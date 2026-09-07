using UnityEngine;
using UnityEngine.UI;

namespace DecoreXR.App
{
    /// <summary>
    /// One colour swatch on the palette: it shows a colour, choosing it paints in that colour, and it
    /// marks itself while that colour is the current one (ADR 0009).
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="PaletteToolButton"/>, with one difference that shapes the whole
    /// component: a tool button knows its tool from the inspector, but a swatch is told its colour at
    /// runtime, because the colours come from <see cref="PaintColorPalette"/> and it is
    /// <see cref="PaletteColorRow"/> that spawns one of these per entry. That is what keeps "add a
    /// colour" to editing one asset rather than copying scene objects and re-laying them out.
    /// <para>
    /// Being configured after it exists, it has to work whichever order that happens in:
    /// <see cref="Bind"/> may land before or after the first <c>OnEnable</c>, so both do the
    /// subscribing and neither assumes the other went first.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class PaletteColorButton : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("The graphic tinted to this swatch's colour — normally the button's own Image.")]
        [SerializeField] private Graphic swatchGraphic;

        [Tooltip("Shown only while this swatch's colour is the active one — the whole of how a " +
                 "swatch says it is the chosen one, since unlike a tool button it must not grey " +
                 "itself out.")]
        [SerializeField] private GameObject selectedIndicator;

        private Button button;
        private PaletteState paletteState;
        private Color swatchColor;

        /// <summary>The colour this swatch chooses. Meaningless until <see cref="Bind"/>.</summary>
        public Color SwatchColor => swatchColor;

        /// <summary>
        /// The button, found on first use rather than cached in <c>Awake</c>.
        /// </summary>
        /// <remarks>
        /// <see cref="Bind"/> is called on a freshly instantiated swatch, and a caller of a public
        /// method should not have to know whether <c>Awake</c> has run yet for it to work. Resolving
        /// here removes that ordering from the contract entirely.
        /// </remarks>
        private Button Control
        {
            get
            {
                if (button == null)
                {
                    button = GetComponent<Button>();

                    if (swatchGraphic == null)
                    {
                        swatchGraphic = button.targetGraphic;
                    }
                }

                return button;
            }
        }

        /// <summary>
        /// Tells this swatch which colour it is and which palette state it writes to. Safe to call
        /// again to re-purpose the swatch, which is what rebuilding the row does.
        /// </summary>
        public void Bind(PaletteState state, Color color)
        {
            Unsubscribe();

            paletteState = state;

            color.a = 1f;
            swatchColor = color;

            // Touch Control first, so a swatch that has not woken up yet still resolves the graphic
            // it is about to tint.
            if (Control != null && swatchGraphic != null)
            {
                swatchGraphic.color = swatchColor;
            }

            Subscribe();
        }

        private void OnEnable()
        {
            // Nothing to subscribe to yet if the row has not bound this swatch. Bind will do it.
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (paletteState == null || !isActiveAndEnabled)
            {
                return;
            }

            // Both Bind and OnEnable call this, in whichever order they happen. Removing first makes
            // the second call a no-op instead of a double subscription.
            Control.onClick.RemoveListener(OnClicked);
            paletteState.ColorChanged -= OnColorChanged;

            Control.onClick.AddListener(OnClicked);
            paletteState.ColorChanged += OnColorChanged;

            Present(paletteState.ActiveColor);
        }

        private void Unsubscribe()
        {
            // Not Control: this also runs as the object is being torn down, by which point the
            // Button may already be gone and asking for it again would only find nothing.
            if (button != null)
            {
                button.onClick.RemoveListener(OnClicked);
            }

            if (paletteState != null)
            {
                paletteState.ColorChanged -= OnColorChanged;
            }
        }

        private void OnClicked() => paletteState.SelectColor(swatchColor);

        private void OnColorChanged(Color active) => Present(active);

        /// <remarks>
        /// Unlike <see cref="PaletteToolButton"/> this leaves <c>interactable</c> alone. A disabled
        /// uGUI button tints its target graphic, and the target graphic here <em>is</em> the colour
        /// the user is judging — greying out the chosen swatch would misreport the very thing the
        /// swatch exists to show. Re-pressing it is harmless anyway: choosing the current colour is
        /// not a change.
        /// </remarks>
        private void Present(Color active)
        {
            active.a = 1f;

            if (selectedIndicator != null)
            {
                selectedIndicator.SetActive(active == swatchColor);
            }
        }
    }
}
