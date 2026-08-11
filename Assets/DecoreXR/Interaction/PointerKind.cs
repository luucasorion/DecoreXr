namespace DecoreXR.Interaction
{
    /// <summary>
    /// Which physical input is behind an <see cref="IPointerSource"/> (ADR 0002).
    /// </summary>
    /// <remarks>
    /// Painting never branches on this — a pointer is a pointer. It exists so the app can tell the
    /// user what is currently driving the ray, and so tuning that genuinely differs between the two
    /// (a controller trigger is crisp, a pinch is not) has something to key off.
    /// </remarks>
    public enum PointerKind
    {
        /// <summary>A tracked controller aiming along its pointing anchor.</summary>
        Controller = 0,

        /// <summary>A tracked hand aiming along its pointer pose, pressing by pinching.</summary>
        Hand = 1,
    }
}
