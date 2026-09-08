using System.Collections.Generic;
using DecoreXR.Core;
using UnityEngine;

namespace DecoreXR.Painting
{
    /// <summary>
    /// Takes paint back off one surface along a path the user dragged — the eraser, and the first
    /// command that subtracts rather than adds.
    /// </summary>
    /// <remarks>
    /// Still a command, and that is the whole point. Erasing does not reach into the history to
    /// delete what was painted; it goes on the end of the one global list like everything else
    /// (ADR 0007), so it takes off whatever the commands before it put down and a command after it
    /// paints over the hole. Undo therefore brings the erased paint back, because the erase is a
    /// thing that was done and can be undone rather than paint that was destroyed — and M8 can
    /// write it to disk like any other command (ADR 0006).
    /// <para>
    /// The path is a polyline in normalized <c>(u,v)</c> and the width is in metres, the same units
    /// <see cref="StrokeCommand"/> uses and for the same reasons (ADR 0003, ADR 0004). It carries no
    /// colour: what an erase leaves behind is the surface's unpainted state, through which
    /// passthrough shows the real wall again, and not a colour anything here could name.
    /// </para>
    /// <para>
    /// Immutable, like every command: the path is copied on the way in, so the gesture that produced
    /// it can go on reusing its own buffer.
    /// </para>
    /// </remarks>
    public sealed class EraserCommand : IPaintCommand
    {
        private readonly string surfaceId;
        private readonly Vector2[] points;
        private readonly float width;

        /// <param name="surfaceId">The surface's anchor id, as <see cref="IPaintCommand.SurfaceId"/>.</param>
        /// <param name="points">
        /// The path in the surface's normalized <c>(u,v)</c>, in drawn order. Copied, not kept.
        /// </param>
        /// <param name="width">The erased band's full width in metres.</param>
        public EraserCommand(string surfaceId, IReadOnlyList<Vector2> points, float width)
        {
            this.surfaceId = surfaceId;
            this.width = width;

            // An empty path rather than a null field, as in StrokeCommand: a command that erases
            // nothing is a real outcome, not a broken one.
            var count = points?.Count ?? 0;
            this.points = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                this.points[i] = points[i];
            }
        }

        /// <inheritdoc />
        public string SurfaceId => surfaceId;

        /// <inheritdoc />
        public string DisplayName => "Erase";

        /// <summary>The erased path in the surface's normalized <c>(u,v)</c>, in drawn order.</summary>
        public IReadOnlyList<Vector2> Points => points;

        /// <summary>The erased band's full width in metres.</summary>
        public float Width => width;

        /// <inheritdoc />
        public void Render(IPaintCanvas canvas)
        {
            canvas.ErasePolyline(points, width);
        }
    }
}
