using System.Collections.Generic;

namespace DecoreXR.Core
{
    /// <summary>
    /// Where <see cref="PaintStore"/> gets its codecs from: the assembly that owns the command types
    /// offers one of these, and the store asks it what it can read and write (ADR 0006).
    /// </summary>
    /// <remarks>
    /// The indirection exists because <c>Core</c> must not name the command types it persists
    /// (architecture §4) — so it cannot construct their codecs either. A source in <c>Painting</c>
    /// can, and is wired to the store in the scene like the surface providers are wired to
    /// <c>PaintRenderer</c>. That is also the extension point: a later assembly with commands of its
    /// own — furniture (ADR 0008) — adds a second source rather than editing anything here.
    /// </remarks>
    public interface IPaintCommandCodecSource
    {
        /// <summary>
        /// The codecs this source offers, one per command type. Read once when the store starts up.
        /// </summary>
        IReadOnlyList<IPaintCommandCodec> Codecs { get; }
    }
}
