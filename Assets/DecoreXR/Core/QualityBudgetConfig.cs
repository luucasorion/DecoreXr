using UnityEngine;

namespace DecoreXR.Core
{
    /// <summary>
    /// Data-driven, per-platform canvas quality budget (ADR 0004, ADR 0012).
    /// The paint canvas resolution, per-wall texture cap, and occlusion toggle live here as
    /// config, never as magic numbers in consuming code (architecture §8.3 "config-stays-config").
    /// Standalone Quest 3 is the default target; a higher PCVR budget stays switchable by config
    /// only, so the paint engine needs no changes to scale quality (ADR 0012).
    /// This is an M0 stub: the shape is intentionally minimal and gets consumed by the Painting
    /// renderer from M3 onward.
    /// </summary>
    [CreateAssetMenu(fileName = "QualityBudgetConfig", menuName = "DecoreXR/Quality Budget Config")]
    public sealed class QualityBudgetConfig : ScriptableObject
    {
        /// <summary>One platform's canvas budget. See ADR 0004 for the standalone defaults.</summary>
        [System.Serializable]
        public struct Budget
        {
            [Tooltip("Canvas resolution in texels per meter of wall surface (ADR 0004 default: 256).")]
            [Min(1)] public int texelsPerMeter;

            [Tooltip("Maximum per-wall texture dimension in pixels (ADR 0004 cap: 1024).")]
            [Min(1)] public int maxTextureSize;

            [Tooltip("Enable environment depth occlusion (ADR 0005). Costs GPU headroom on standalone.")]
            public bool occlusionEnabled;
        }

        [Header("Standalone (Quest 3) — primary target, ADR 0004")]
        [SerializeField]
        private Budget standalone = new Budget { texelsPerMeter = 256, maxTextureSize = 1024, occlusionEnabled = true };

        // PCVR budget exists so quality can scale by config only (ADR 0012). Concrete PCVR values
        // are NOT decided yet — they await their own ADR when PCVR is actually built (post-MVP), so
        // this stub mirrors the standalone defaults rather than asserting invented numbers.
        [Header("PCVR (Quest Link / PC OpenXR) — placeholder = standalone until a PCVR ADR, ADR 0012")]
        [SerializeField]
        private Budget pcvr = new Budget { texelsPerMeter = 256, maxTextureSize = 1024, occlusionEnabled = true };

        /// <summary>The standalone-Android budget (ADR 0004 defaults).</summary>
        public Budget Standalone => standalone;

        /// <summary>The PCVR budget; raised via config, no engine changes (ADR 0012).</summary>
        public Budget Pcvr => pcvr;

        /// <summary>
        /// The budget for the current runtime platform. Standalone-first (ADR 0012): mobile
        /// (Android/Quest standalone) uses <see cref="Standalone"/>, everything else <see cref="Pcvr"/>.
        /// </summary>
        public Budget Active => Application.isMobilePlatform ? standalone : pcvr;
    }
}
