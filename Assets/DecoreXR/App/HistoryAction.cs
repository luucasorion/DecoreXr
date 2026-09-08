namespace DecoreXR.App
{
    /// <summary>
    /// Which way along the paint history a palette control moves the user (ADR 0007).
    /// </summary>
    /// <remarks>
    /// Not a <see cref="PaintTool"/>. A tool is a standing choice that says what the <em>next</em>
    /// press on a wall will paint; undo and redo happen the moment they are pressed and leave that
    /// choice exactly as it was. Keeping them out of the tool enum is what stops the palette from
    /// having a "current tool" the user can never actually paint with.
    /// </remarks>
    public enum HistoryAction
    {
        /// <summary>Take back the last thing the user did, wherever they did it.</summary>
        Undo = 0,

        /// <summary>Put back the most recently undone thing.</summary>
        Redo = 1,
    }
}
