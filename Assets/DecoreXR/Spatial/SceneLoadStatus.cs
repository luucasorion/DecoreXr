namespace DecoreXR.Spatial
{
    /// <summary>
    /// Outcome of loading the room's scene model (ADR 0001). This is the vocabulary the rest of
    /// the app reasons about: <c>App</c> reacts to a denied permission or an unscanned room
    /// without ever referencing an MRUK type (architecture §4, ADR 0010).
    /// Every failure is an explicit, communicable state — never a silent no-op (architecture §8.5).
    /// </summary>
    public enum SceneLoadStatus
    {
        /// <summary>No load has been attempted yet.</summary>
        NotStarted,

        /// <summary>A permission request and/or scene load is in flight.</summary>
        Loading,

        /// <summary>The scene model loaded and at least one room is available.</summary>
        Ready,

        /// <summary>The user denied (or has not granted) the scene permission.</summary>
        PermissionDenied,

        /// <summary>Permission is granted but the user has not run Space Setup, so there is no room.</summary>
        NoSceneFound,

        /// <summary>The scene system was unavailable or errored — e.g. no MRUK instance in the scene.</summary>
        Failed,
    }

    /// <summary>
    /// Convenience predicates over <see cref="SceneLoadStatus"/> so callers do not re-enumerate
    /// the failure cases at every call site.
    /// </summary>
    public static class SceneLoadStatusExtensions
    {
        /// <summary>True when the load finished, successfully or not.</summary>
        public static bool IsTerminal(this SceneLoadStatus status) =>
            status != SceneLoadStatus.NotStarted && status != SceneLoadStatus.Loading;

        /// <summary>
        /// True when the user has no usable scene and should be offered Space Setup or the manual
        /// plane fallback (ADR 0010).
        /// </summary>
        public static bool NeedsFallback(this SceneLoadStatus status) =>
            status == SceneLoadStatus.PermissionDenied ||
            status == SceneLoadStatus.NoSceneFound ||
            status == SceneLoadStatus.Failed;
    }
}
