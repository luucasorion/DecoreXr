using System.Collections.Generic;
using DecoreXR.Core;
using UnityEngine;

namespace DecoreXR.Painting
{
    /// <summary>
    /// Paints a freehand stroke on one surface: a path the user dragged, painted at a constant
    /// width. The first command whose shape the user draws rather than parameterises.
    /// </summary>
    /// <remarks>
    /// The path is a polyline in normalized <c>(u,v)</c> and the width is in metres — the same
    /// pairing <see cref="CircleCommand"/> uses, and for the same reason (ADR 0003, ADR 0004):
    /// <c>(u,v)</c> is the only frame a wall of unknown size has and is what a raycast hit reports,
    /// while a width in <c>(u,v)</c> would be a different real thickness on every wall and on each
    /// axis. Neither unit is in texels, so both survive the canvas being rebuilt at a different
    /// density.
    /// <para>
    /// One command for the whole stroke, not one per sample. That is the granularity undo has to
    /// have (ADR 0007): a user who undoes after drawing a line means the line, not the last
    /// millimetre of it. It also keeps the history — and M8's file on disk (ADR 0006) — the size of
    /// what was drawn rather than of how long it took to draw.
    /// </para>
    /// <para>
    /// Immutable, like every command: the path is copied on the way in, so the gesture that
    /// produced it can go on reusing its own buffer without rewriting paint the user has already
    /// finished.
    /// </para>
    /// </remarks>
    public sealed class StrokeCommand : IPaintCommand
    {
        private readonly string surfaceId;
        private readonly Vector2[] points;
        private readonly float width;
        private readonly Color32 strokeColor;

        /// <param name="surfaceId">The surface's anchor id, as <see cref="IPaintCommand.SurfaceId"/>.</param>
        /// <param name="points">
        /// The path in the surface's normalized <c>(u,v)</c>, in drawn order. Copied, not kept.
        /// </param>
        /// <param name="width">Stroke width in metres.</param>
        /// <param name="strokeColor">The colour to paint.</param>
        public StrokeCommand(string surfaceId, IReadOnlyList<Vector2> points, float width, Color32 strokeColor)
        {
            this.surfaceId = surfaceId;
            this.width = width;
            this.strokeColor = strokeColor;

            // An empty stroke rather than a null field: Render and Points then need no null case,
            // and a command that draws nothing is a real outcome (a gesture cancelled before it
            // sampled anything), not a broken one.
            var count = points?.Count ?? 0;
            this.points = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                this.points[i] = points[i];
            }
        }

        /// <inheritdoc />
        public string SurfaceId => surfaceId;

        /// <summary>The stroke's path in the surface's normalized <c>(u,v)</c>, in drawn order.</summary>
        public IReadOnlyList<Vector2> Points => points;

        /// <summary>Stroke width in metres.</summary>
        public float Width => width;

        /// <summary>The colour this command paints.</summary>
        public Color32 StrokeColor => strokeColor;

        /// <inheritdoc />
        public void Render(IPaintCanvas canvas)
        {
            canvas.StrokePolyline(points, width, strokeColor);
        }
    }
}
