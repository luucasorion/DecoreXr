namespace DecoreXR.Core
{
    /// <summary>
    /// One command as it is written to and read back from disk: the command itself, plus where it
    /// stood in the one global history when it was saved (ADR 0006, ADR 0007).
    /// </summary>
    /// <remarks>
    /// The order is carried because the files are keyed per wall (ADR 0006) while the history is one
    /// list for the whole room (ADR 0007). Without it, a reload would restore wall by wall, and
    /// "undo the last thing I did" would afterwards mean "undo the last thing I did on the last wall
    /// the loader happened to read" — which is not the order the user did anything in. With it, the
    /// per-wall files merge back into the sequence they were saved from.
    /// <para>
    /// The number is the command's index in the saved history, so it is only meaningful against the
    /// other commands from the same save. It is not an id and nothing looks a command up by it.
    /// </para>
    /// </remarks>
    public readonly struct SavedCommand
    {
        public SavedCommand(int order, IPaintCommand command)
        {
            Order = order;
            Command = command;
        }

        /// <summary>The command's position in the global history it was saved from, lowest first.</summary>
        public int Order { get; }

        /// <summary>The command itself.</summary>
        public IPaintCommand Command { get; }
    }
}
