using System;

namespace DecoreXR.Core
{
    /// <summary>
    /// Turns one kind of command into the text that goes in its wall's file, and back again
    /// (ADR 0006). One codec per command type.
    /// </summary>
    /// <remarks>
    /// This is the same seam <see cref="IPaintCanvas"/> is, one layer over: there, a command draws
    /// itself so that no renderer has to know every command type; here, a codec per type keeps
    /// <c>Core</c> from having to know any of them. It has to be that way round — the command types
    /// live in <c>Painting</c>, which depends on <c>Core</c> and not the reverse (architecture §4) —
    /// and it is what keeps ADR 0003's promise intact through persistence: a tool added after the MVP
    /// arrives as a command type plus a codec, with nothing in <c>Core</c> to revisit.
    /// <para>
    /// Codecs are handed to <see cref="PaintStore"/> by an <see cref="IPaintCommandCodecSource"/>
    /// in the assembly that owns the command types. They are expected to be stateless: the store
    /// keeps one of each for the session and uses it for every command of that type.
    /// </para>
    /// </remarks>
    public interface IPaintCommandCodec
    {
        /// <summary>
        /// The short tag written into the file to say which kind of command an entry is — "fill",
        /// "circle", "stroke", "erase".
        /// </summary>
        /// <remarks>
        /// Deliberately its own string rather than the C# type name. It is on-disk format: a class
        /// that gets renamed or moved must not make a user's saved room unreadable, and a tag that
        /// was the type name would tie the two together silently. Once shipped, a tag never changes
        /// meaning.
        /// </remarks>
        string TypeId { get; }

        /// <summary>
        /// The concrete command type this codec writes, so the store can find the right codec for a
        /// command it is given.
        /// </summary>
        Type CommandType { get; }

        /// <summary>
        /// Writes the command's own data — everything except the surface it is on, which the file
        /// already says once for all of its entries.
        /// </summary>
        /// <returns>
        /// The entry's payload, or null if the command cannot be written. Returning null loses that
        /// one command rather than the wall, and the store reports it.
        /// </returns>
        string Write(IPaintCommand command);

        /// <summary>
        /// Rebuilds a command from a payload <see cref="Write"/> produced, on the surface the file
        /// belongs to.
        /// </summary>
        /// <param name="surfaceId">
        /// The wall's anchor UUID, from the file rather than the payload — a command's surface is
        /// which file it is in, so it is stored once instead of once per entry.
        /// </param>
        /// <param name="payload">The entry's payload.</param>
        /// <returns>
        /// The command, or null if the payload does not describe one. Null is a skipped entry, not a
        /// thrown exception: one unreadable command must not cost the user the rest of the wall
        /// (architecture §8.5).
        /// </returns>
        IPaintCommand Read(string surfaceId, string payload);
    }
}
