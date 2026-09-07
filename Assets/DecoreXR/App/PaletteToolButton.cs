using UnityEngine;
using UnityEngine.UI;

namespace DecoreXR.App
{
    /// <summary>
    /// Makes one palette button mean one tool: pressing it chooses that tool, and it shows whether
    /// that tool is the current one (ADR 0009).
    /// </summary>
    /// <remarks>
    /// One component per button rather than one manager holding a list, because then adding a tool
    /// to the palette is adding a button in the scene and picking its tool in the inspector — no
    /// code, and nothing to keep in step with the tool set. It is also what keeps
    /// <see cref="WristPalette"/> free of any opinion about what the panel contains.
    /// <para>
    /// The button writes to <see cref="PaletteState"/> and reads its notifications back, rather than
    /// tracking selection itself. That is what makes the panel agree with the choice however it was
    /// made — by another button, by the inspector default, or by something that changes tools without
    /// the palette in M5-T4.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class PaletteToolButton : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The palette state this button writes its tool into.")]
        [SerializeField] private PaletteState paletteState;

        [Tooltip("The tool this button chooses.")]
        [SerializeField] private PaintTool tool = PaintTool.Fill;

        [Header("UI")]
        [Tooltip("Shown only while this button's tool is the active one — an outline, a tick, or " +
                 "whatever the panel uses to mark the current choice. Optional: without it the " +
                 "button still works, it just cannot say it is the chosen one.")]
        [SerializeField] private GameObject selectedIndicator;

        private Button button;

        /// <summary>The tool this button chooses.</summary>
        public PaintTool Tool => tool;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void Reset()
        {
            paletteState = FindAnyObjectByType<PaletteState>();
        }

        private void OnEnable()
        {
            if (paletteState == null)
            {
                Debug.LogError(
                    $"[{nameof(PaletteToolButton)}] No {nameof(PaletteState)} assigned, so pressing " +
                    "this button would choose nothing. Assign one in the inspector.", this);
                enabled = false;
                return;
            }

            button.onClick.AddListener(OnClicked);
            paletteState.ToolChanged += OnToolChanged;

            // The panel is built hidden and shown later, so the first thing it does on appearing has
            // to be to catch up with whatever the choice already is.
            Present(paletteState.ActiveTool);
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClicked);
            }

            if (paletteState != null)
            {
                paletteState.ToolChanged -= OnToolChanged;
            }
        }

        private void OnClicked() => paletteState.SelectTool(tool);

        private void OnToolChanged(PaintTool active) => Present(active);

        private void Present(PaintTool active)
        {
            if (selectedIndicator != null)
            {
                selectedIndicator.SetActive(active == tool);
            }

            // The chosen tool's own button is not a thing left to press. Greying it out is also the
            // fallback marker when there is no indicator to show.
            button.interactable = active != tool;
        }
    }
}
