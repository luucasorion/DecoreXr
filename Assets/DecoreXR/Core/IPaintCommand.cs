namespace DecoreXR.Core
{
    /// <summary>
    /// One thing the user did to one surface — a solid fill now, a stroke or a circle or an erase
    /// later. Commands are the source of truth for what a wall looks like (ADR 0003): the per-wall
    /// texture is only a render of its command list, and can be thrown away and rebuilt at any
    /// resolution the quality budget asks for (ADR 0004).
    /// </summary>
    /// <remarks>
    /// A command draws itself into an <see cref="IPaintCanvas"/> rather than being decoded by a
    /// renderer that has to know every command type. That is what makes ADR 0003's promise hold:
    /// adding a tool means adding a command type, with no renderer to redesign each time.
    /// <para>
    /// Implementations are expected to be immutable. The history keeps a command for the whole
    /// session and re-renders from it (ADR 0007), so one that mutates after being pushed would
    /// quietly rewrite what the user already did.
    /// </para>
    /// </remarks>
    public interface IPaintCommand
    {
        /// <summary>
        /// Which surface this command paints, matching <c>IPaintableSurface.Id</c> — the wall's
        /// spatial anchor UUID.
        /// </summary>
        /// <remarks>
        /// An identity rather than a surface reference, for three reasons that point the same way:
        /// the stack is global across walls so every command has to say which one it meant
        /// (ADR 0007); commands outlive the surface objects across a save and load (ADR 0006); and
        /// <c>Core</c> must not depend on <c>Spatial</c>, where <c>IPaintableSurface</c> lives
        /// (architecture §4).
        /// </remarks>
        string SurfaceId { get; }

        /// <summary>
        /// Draws this command onto one surface's canvas. Called while re-rendering that surface, in
        /// the order the commands were pushed, so later commands paint over earlier ones.
        /// </summary>
        void Render(IPaintCanvas canvas);
    }
}
