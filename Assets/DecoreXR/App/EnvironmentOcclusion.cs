using DecoreXR.Core;
using Meta.XR.EnvironmentDepth;
using UnityEngine;

namespace DecoreXR.App
{
    /// <summary>
    /// Turns environment depth occlusion on or off from the quality budget, so paint is occluded by
    /// whatever is really in front of the wall — a hand, the controller, furniture (ADR 0005).
    /// </summary>
    /// <remarks>
    /// This lives in <c>App</c> rather than <c>Painting</c> for two reasons. What it switches is
    /// global render state — <see cref="EnvironmentDepthManager"/> sets a shader keyword for the
    /// whole frame, not per canvas — and that makes it a composition concern (architecture §4).
    /// And it keeps the Meta depth SDK out of <c>Painting</c>, which today references nothing but
    /// <c>Core</c> and <c>Spatial</c>; <c>Painting</c>'s side of occlusion is the shader on the
    /// canvas material, which needs no C# reference at all.
    /// <para>
    /// Whether occlusion is on is config, never a build-time fact (architecture §8.3): it reads
    /// <see cref="QualityBudgetConfig.Budget.occlusionEnabled"/> and
    /// <see cref="QualityBudgetConfig.Budget.occlusionQuality"/> from the active per-platform
    /// budget, so standalone can drop what PCVR keeps without touching the paint engine
    /// (ADR 0004, ADR 0012). That also makes "occlusion cost too much on device" a one-field change
    /// rather than a code change, which is the outcome M4-T2 may well reach (ADR 0011).
    /// </para>
    /// <para>
    /// Occlusion is a visual improvement, not a prerequisite for painting. So every way this can
    /// fail — no depth support on the platform, the OpenXR occlusion feature left off, no manager
    /// in the scene — degrades to painting without occlusion and says so once, rather than
    /// stopping the app (architecture §8.5). The paint shader's macros compile to a no-op when the
    /// keywords are unset, so unoccluded paint is a supported state and not a broken one.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnvironmentOcclusion : MonoBehaviour
    {
        [Header("Budget")]
        [Tooltip("Where the occlusion toggle and quality come from (ADR 0004/0005). The active " +
                 "per-platform budget is used, so quality scales by config alone (ADR 0012).")]
        [SerializeField] private QualityBudgetConfig qualityBudget;

        [Header("Depth")]
        [Tooltip("The Meta EnvironmentDepthManager this drives — the Occlusion Building Block's " +
                 "manager in the scene (ADR 0013). Leave empty to find the one in the scene.")]
        [SerializeField] private EnvironmentDepthManager depthManager;

        /// <summary>
        /// True when depth occlusion is actually running: the budget asked for it, the platform
        /// supports it, and a manager is driving it. False whenever paint is drawing unoccluded, for
        /// any of the reasons above.
        /// </summary>
        public bool IsOccluding { get; private set; }

        private void OnEnable()
        {
            if (qualityBudget == null)
            {
                Debug.LogError(
                    $"[{nameof(EnvironmentOcclusion)}] No {nameof(QualityBudgetConfig)} assigned, so " +
                    "there is nothing to read the occlusion toggle from. Paint will draw unoccluded. " +
                    "Assign the budget asset in the inspector.", this);
                return;
            }

            Apply(qualityBudget.Active);
        }

        private void OnDisable()
        {
            // The keyword this component set is global and outlives it, so leaving it on would keep
            // every canvas testing against a depth texture nothing is updating any more
            // (architecture §8.4).
            Stop();
        }

        /// <summary>
        /// Re-reads the budget and applies it. Public so a later quality change — M4-T2 turning
        /// occlusion off after measuring the frame cost, or a PCVR/standalone switch (ADR 0012) —
        /// takes effect without a scene reload.
        /// </summary>
        public void Reapply()
        {
            if (qualityBudget == null)
            {
                // OnEnable already said why.
                return;
            }

            Apply(qualityBudget.Active);
        }

        private void Apply(QualityBudgetConfig.Budget budget)
        {
            if (!budget.occlusionEnabled)
            {
                // Not a failure: a budget that cannot afford occlusion is exactly what ADR 0004 is
                // for, so this path is silent.
                Stop();
                return;
            }

            if (!EnvironmentDepthManager.IsSupported)
            {
                // The SDK logs the specific cause itself (no XR loader, or the "Meta Quest:
                // Occlusion" OpenXR feature left disabled — ADR 0017), so this says what it means
                // for the user rather than repeating the diagnosis.
                Debug.LogWarning(
                    $"[{nameof(EnvironmentOcclusion)}] Environment depth is not supported here, so " +
                    "paint will not be occluded by real objects. Expected in the Editor without the " +
                    "XR Simulator; on device it points at the depth setup (ADR 0005, ADR 0017).", this);
                Stop();
                return;
            }

            if (!ResolveManager())
            {
                Stop();
                return;
            }

            depthManager.enabled = true;
            depthManager.OcclusionShadersMode = ModeFor(budget.occlusionQuality);
            IsOccluding = true;
        }

        /// <summary>
        /// Leaves the scene drawing unoccluded paint: keywords cleared and the depth manager idle,
        /// so nothing is paying for a depth texture no shader is reading.
        /// </summary>
        private void Stop()
        {
            IsOccluding = false;

            if (depthManager == null)
            {
                return;
            }

            depthManager.OcclusionShadersMode = OcclusionShadersMode.None;
            depthManager.enabled = false;
        }

        private bool ResolveManager()
        {
            if (depthManager != null)
            {
                return true;
            }

            // Only when unassigned, and only once per enable. The manager is a scene singleton the
            // Occlusion Building Block installs (ADR 0013); finding it beats failing because an
            // inspector slot was missed.
            depthManager = FindAnyObjectByType<EnvironmentDepthManager>(FindObjectsInactive.Include);

            if (depthManager == null)
            {
                Debug.LogError(
                    $"[{nameof(EnvironmentOcclusion)}] The budget asks for occlusion but there is no " +
                    $"{nameof(EnvironmentDepthManager)} in the scene, so paint will draw unoccluded. " +
                    "Add the Meta Occlusion Building Block (ADR 0005, ADR 0013).", this);
                return false;
            }

            return true;
        }

        private static OcclusionShadersMode ModeFor(QualityBudgetConfig.OcclusionQuality quality)
        {
            switch (quality)
            {
                case QualityBudgetConfig.OcclusionQuality.Hard:
                    return OcclusionShadersMode.HardOcclusion;
                case QualityBudgetConfig.OcclusionQuality.Soft:
                    return OcclusionShadersMode.SoftOcclusion;
                default:
                    // A budget asset saved by a newer build than this one. Occlude softly rather
                    // than not at all, and say that the value was not understood.
                    Debug.LogWarning(
                        $"[{nameof(EnvironmentOcclusion)}] Unknown occlusion quality '{quality}' in the " +
                        "budget; using Soft.");
                    return OcclusionShadersMode.SoftOcclusion;
            }
        }
    }
}
