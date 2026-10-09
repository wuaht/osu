// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics.Primitives;
using osu.Game.Rulesets.Osu.Edit.Checks;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// Determines whether objects go offscreen, which isn't allowed for ranked beatmaps.
    /// </summary>
    public static class OffscreenObjects
    {
        // The bounding box of the screen in gameplay on 4:3 aspect ratio, in gamefield coordinates.
        // The same as in CheckOffscreenObjects.
        public const float MIN_X = -67;
        public const float MIN_Y = -60;
        public const float MAX_X = 579;
        public const float MAX_Y = 428;

        /// <summary>
        /// Objects closer than this to the edge of the screen are also considered offscreen.
        /// The bounds above are measured rather than exact, and the game rounds positions when rendering,
        /// so such objects can still be partially offscreen in-game (Mapset Verifier reports them as "borderline").
        /// </summary>
        public const float BORDERLINE_MARGIN = 1;

        /// <summary>
        /// Determines whether an object goes offscreen on 4:3 aspect ratio, including any part of a slider's body.
        /// </summary>
        /// <param name="hitObject">The object to check.</param>
        /// <param name="bounds">The bounding box of the object, including its radius, in gamefield coordinates.</param>
        /// <returns>Whether the object goes offscreen or is within <see cref="BORDERLINE_MARGIN"/> of it. Always <c>false</c> for objects other than circles and sliders.</returns>
        /// <remarks>
        /// Unlike <see cref="CheckOffscreenObjects"/>, which samples slider paths at intervals, every vertex of the calculated slider path is considered.
        /// As the path is drawn as straight lines between these vertices, this is exact.
        /// </remarks>
        public static bool IsOffscreen(OsuHitObject hitObject, out RectangleF bounds)
        {
            Vector2 min, max;

            switch (hitObject)
            {
                case HitCircle circle:
                    min = max = circle.StackedPosition;
                    break;

                case Slider slider:
                {
                    var path = new List<Vector2>();
                    slider.Path.GetPathToProgress(path, 0, 1);

                    min = max = slider.StackedPosition;

                    foreach (var vertex in path)
                    {
                        Vector2 position = slider.StackedPosition + vertex;
                        min = Vector2.ComponentMin(min, position);
                        max = Vector2.ComponentMax(max, position);
                    }

                    break;
                }

                default:
                    bounds = RectangleF.Empty;
                    return false;
            }

            float radius = (float)hitObject.Radius;

            min -= new Vector2(radius);
            max += new Vector2(radius);

            bounds = new RectangleF(min, max - min);

            return min.X < MIN_X + BORDERLINE_MARGIN || min.Y < MIN_Y + BORDERLINE_MARGIN
                                                       || max.X > MAX_X - BORDERLINE_MARGIN || max.Y > MAX_Y - BORDERLINE_MARGIN;
        }
    }
}
