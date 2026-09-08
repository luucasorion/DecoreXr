using System;
using System.Collections.Generic;
using UnityEngine;

namespace DecoreXR.Core
{
    /// <summary>
    /// The codecs the app can read and write, looked up both ways: by the tag in a file, and by the
    /// type of a command about to be saved (ADR 0006).
    /// </summary>
    /// <remarks>
    /// A plain object rather than a component or a static: <see cref="PaintStore"/> owns one and
    /// fills it from the sources wired to it in the scene, so nothing reaches a global registry from
    /// anywhere (architecture §4).
    /// <para>
    /// Both kinds of duplicate are refused rather than allowed to overwrite. Two codecs claiming one
    /// tag would make which of them read a file depend on registration order, and two claiming one
    /// command type would do the same for writing it — either way the app would round-trip paint
    /// correctly only by luck.
    /// </para>
    /// </remarks>
    public sealed class PaintCommandCodecRegistry
    {
        private readonly Dictionary<string, IPaintCommandCodec> byTypeId =
            new Dictionary<string, IPaintCommandCodec>(StringComparer.Ordinal);

        private readonly Dictionary<Type, IPaintCommandCodec> byCommandType =
            new Dictionary<Type, IPaintCommandCodec>();

        /// <summary>How many codecs are registered.</summary>
        public int Count => byTypeId.Count;

        /// <summary>
        /// Adds a codec. Rejects one that is malformed or that collides with a codec already
        /// registered, reporting why rather than replacing it.
        /// </summary>
        /// <returns>True if the codec was registered.</returns>
        public bool Register(IPaintCommandCodec codec)
        {
            if (codec == null)
            {
                Debug.LogError($"[{nameof(PaintCommandCodecRegistry)}] Refused a null codec.");
                return false;
            }

            if (string.IsNullOrEmpty(codec.TypeId))
            {
                Debug.LogError(
                    $"[{nameof(PaintCommandCodecRegistry)}] Refused {codec.GetType().Name}: a codec " +
                    $"with no {nameof(IPaintCommandCodec.TypeId)} could write entries nothing could " +
                    "ever read back.");
                return false;
            }

            if (codec.CommandType == null)
            {
                Debug.LogError(
                    $"[{nameof(PaintCommandCodecRegistry)}] Refused codec '{codec.TypeId}': it names " +
                    $"no {nameof(IPaintCommandCodec.CommandType)}, so no command could be matched to " +
                    "it when saving.");
                return false;
            }

            if (byTypeId.TryGetValue(codec.TypeId, out var claimingTag))
            {
                Debug.LogError(
                    $"[{nameof(PaintCommandCodecRegistry)}] Refused {codec.GetType().Name}: " +
                    $"{claimingTag.GetType().Name} already writes entries tagged '{codec.TypeId}'. " +
                    "Two codecs sharing a tag would make which one reads a file depend on wiring " +
                    "order.");
                return false;
            }

            if (byCommandType.TryGetValue(codec.CommandType, out var claimingType))
            {
                Debug.LogError(
                    $"[{nameof(PaintCommandCodecRegistry)}] Refused {codec.GetType().Name}: " +
                    $"{claimingType.GetType().Name} already writes " +
                    $"{codec.CommandType.Name}.");
                return false;
            }

            byTypeId.Add(codec.TypeId, codec);
            byCommandType.Add(codec.CommandType, codec);
            return true;
        }

        /// <summary>
        /// Forgets every registered codec, so the registry can be filled again from scratch — what a
        /// store does when its wiring is re-read rather than adding to what it already had.
        /// </summary>
        public void Clear()
        {
            byTypeId.Clear();
            byCommandType.Clear();
        }

        /// <summary>Finds the codec that reads entries carrying <paramref name="typeId"/>.</summary>
        /// <remarks>
        /// A miss is a real case, not a bug: a file written by a build that had a tool this one does
        /// not. The caller skips that entry and says so.
        /// </remarks>
        public bool TryGetByTypeId(string typeId, out IPaintCommandCodec codec)
        {
            if (string.IsNullOrEmpty(typeId))
            {
                codec = null;
                return false;
            }

            return byTypeId.TryGetValue(typeId, out codec);
        }

        /// <summary>Finds the codec that writes <paramref name="command"/>.</summary>
        /// <remarks>
        /// Matched on the command's exact type, not on assignability. A subclass of a persisted
        /// command would otherwise be written as its base and come back as the wrong thing — losing
        /// whatever the subclass added, silently.
        /// </remarks>
        public bool TryGetFor(IPaintCommand command, out IPaintCommandCodec codec)
        {
            if (command == null)
            {
                codec = null;
                return false;
            }

            return byCommandType.TryGetValue(command.GetType(), out codec);
        }
    }
}
