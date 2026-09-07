namespace DecoreXR.Interaction
{
    /// <summary>
    /// Execution order for the interaction chain, whose stages have to run in a fixed sequence
    /// within a frame: each device source samples itself, the router picks among them, selection
    /// raycasts with the winner's ray, and gestures read that hit.
    /// </summary>
    /// <remarks>
    /// Unity does not order components by default, so without this the router would forward last
    /// frame's aim and selection would act on the frame before that. Stated as one set of constants
    /// rather than scattered literals so the sequence is legible in one place.
    /// </remarks>
    internal static class InteractionExecutionOrder
    {
        /// <summary>Device-backed pointer sources — they depend on nothing but the device.</summary>
        internal const int PointerSource = 100;

        /// <summary>The router, which reads the sources.</summary>
        internal const int PointerRouter = 200;

        /// <summary>Selection, which reads the router.</summary>
        internal const int Selection = 300;

        /// <summary>
        /// Gestures, which read the hit selection already computed this frame rather than raycasting
        /// again.
        /// </summary>
        internal const int SurfaceGesture = 400;
    }
}
