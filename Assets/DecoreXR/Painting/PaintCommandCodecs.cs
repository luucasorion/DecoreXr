using System;
using System.Collections.Generic;
using DecoreXR.Core;
using UnityEngine;

namespace DecoreXR.Painting
{
    /// <summary>
    /// Teaches <see cref="PaintStore"/> to read and write this assembly's commands: one codec for
    /// each of the MVP's four tools (ADR 0006).
    /// </summary>
    /// <remarks>
    /// The codecs have to live here rather than in <c>Core</c>, because writing a command means
    /// knowing its fields and <c>Core</c> must not name the command types (architecture §4). Wired
    /// to the store in the scene, the same way the surface providers are wired to
    /// <see cref="PaintRenderer"/>.
    /// <para>
    /// Kept together in one file, one small class each, because they are the same decision made four
    /// times over and adding a tool means adding a fifth here. Each owns a private serializable shape
    /// for its own fields, so what a command writes down stays next to the command's own definition
    /// of what it means — and no shared shape has to grow a field every time a tool arrives, which is
    /// the trap ADR 0003 exists to avoid.
    /// </para>
    /// <para>
    /// What each writes is exactly the constructor arguments the command needs back, minus the
    /// surface id, which the file already states once for all of its entries. Units go to disk as
    /// they are held in memory — <c>(u,v)</c> normalized and sizes in metres — so a room saved at one
    /// canvas resolution reloads correctly at another (ADR 0004).
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PaintCommandCodecs : MonoBehaviour, IPaintCommandCodecSource
    {
        // Stateless and immutable, so one of each serves every store in the scene and there is
        // nothing per-instance to allocate.
        private static readonly IPaintCommandCodec[] All =
        {
            new FillCodec(),
            new CircleCodec(),
            new StrokeCodec(),
            new EraserCodec(),
        };

        /// <inheritdoc />
        public IReadOnlyList<IPaintCommandCodec> Codecs => All;

        private sealed class FillCodec : IPaintCommandCodec
        {
            public string TypeId => "fill";

            public Type CommandType => typeof(FillCommand);

            public string Write(IPaintCommand command)
            {
                return command is FillCommand fill
                    ? JsonUtility.ToJson(new Data { color = fill.FillColor })
                    : null;
            }

            public IPaintCommand Read(string surfaceId, string payload)
            {
                var data = JsonUtility.FromJson<Data>(payload);
                return data == null ? null : new FillCommand(surfaceId, data.color);
            }

            [Serializable]
            private sealed class Data
            {
                public Color32 color;
            }
        }

        private sealed class CircleCodec : IPaintCommandCodec
        {
            public string TypeId => "circle";

            public Type CommandType => typeof(CircleCommand);

            public string Write(IPaintCommand command)
            {
                return command is CircleCommand circle
                    ? JsonUtility.ToJson(new Data
                    {
                        center = circle.Center,
                        radius = circle.Radius,
                        color = circle.CircleColor,
                    })
                    : null;
            }

            public IPaintCommand Read(string surfaceId, string payload)
            {
                var data = JsonUtility.FromJson<Data>(payload);
                return data == null
                    ? null
                    : new CircleCommand(surfaceId, data.center, data.radius, data.color);
            }

            [Serializable]
            private sealed class Data
            {
                public Vector2 center;
                public float radius;
                public Color32 color;
            }
        }

        private sealed class StrokeCodec : IPaintCommandCodec
        {
            public string TypeId => "stroke";

            public Type CommandType => typeof(StrokeCommand);

            public string Write(IPaintCommand command)
            {
                if (!(command is StrokeCommand stroke))
                {
                    return null;
                }

                return JsonUtility.ToJson(new Data
                {
                    points = ToArray(stroke.Points),
                    width = stroke.Width,
                    color = stroke.StrokeColor,
                });
            }

            public IPaintCommand Read(string surfaceId, string payload)
            {
                var data = JsonUtility.FromJson<Data>(payload);
                return data == null
                    ? null
                    : new StrokeCommand(surfaceId, data.points, data.width, data.color);
            }

            [Serializable]
            private sealed class Data
            {
                public Vector2[] points;
                public float width;
                public Color32 color;
            }
        }

        private sealed class EraserCodec : IPaintCommandCodec
        {
            public string TypeId => "erase";

            public Type CommandType => typeof(EraserCommand);

            public string Write(IPaintCommand command)
            {
                if (!(command is EraserCommand erase))
                {
                    return null;
                }

                return JsonUtility.ToJson(new Data
                {
                    points = ToArray(erase.Points),
                    width = erase.Width,
                });
            }

            public IPaintCommand Read(string surfaceId, string payload)
            {
                var data = JsonUtility.FromJson<Data>(payload);
                return data == null ? null : new EraserCommand(surfaceId, data.points, data.width);
            }

            [Serializable]
            private sealed class Data
            {
                public Vector2[] points;
                public float width;
            }
        }

        /// <summary>
        /// Copies a path into the array shape <see cref="JsonUtility"/> serializes. A command holds
        /// its points as an <see cref="IReadOnlyList{T}"/>, which Unity's serializer has no notion of.
        /// </summary>
        /// <remarks>
        /// Allocating on save is the right trade here: it happens once per command when the user saves
        /// a room, not on the frames where a stroke is being drawn (architecture §7).
        /// </remarks>
        private static Vector2[] ToArray(IReadOnlyList<Vector2> points)
        {
            var count = points?.Count ?? 0;
            var array = new Vector2[count];

            for (var i = 0; i < count; i++)
            {
                array[i] = points[i];
            }

            return array;
        }
    }
}
