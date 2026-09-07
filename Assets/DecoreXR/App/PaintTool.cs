namespace DecoreXR.App
{
    /// <summary>
    /// Which painting tool the next press applies (ADR 0009's "tool" half of the palette).
    /// </summary>
    /// <remarks>
    /// Lives in <c>App</c> rather than <c>Painting</c> on purpose. It is a statement about what the
    /// user has chosen in the UI, not about how paint is represented: <c>Painting</c> knows only
    /// command types, and nothing there should branch on a tool (ADR 0003, architecture §4). The
    /// composition in <c>App</c> is what turns a chosen tool plus a hit into the matching
    /// <c>IPaintCommand</c> (architecture §6 step 5).
    /// <para>
    /// One entry per tool the milestone actually has.
    /// </para>
    /// </remarks>
    public enum PaintTool
    {
        /// <summary>Paint the whole chosen surface one colour.</summary>
        Fill = 0,

        /// <summary>Paint a circle on the chosen surface where the pointer is aimed.</summary>
        Circle = 1,

        /// <summary>Paint a freehand stroke along the path the pointer is dragged.</summary>
        Brush = 2,

        /// <summary>Take paint back off along the path the pointer is dragged.</summary>
        Eraser = 3,
    }
}
