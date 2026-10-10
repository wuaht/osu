// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Utils;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Maps between time and position in the hitsound editor, with the current time at the centre.
    /// </summary>
    public partial class HitsoundTimeline : Component
    {
        public const float MIN_ZOOM = 0.03f;
        public const float MAX_ZOOM = 2.5f;
        public const float DEFAULT_ZOOM = 0.3f;

        // static, so that the zoom is kept after leaving and re-entering the editor.
        private static float lastZoom = DEFAULT_ZOOM;

        /// <summary>
        /// The target zoom in pixels per millisecond. <see cref="CurrentZoom"/> follows it smoothly.
        /// </summary>
        public readonly BindableFloat Zoom = new BindableFloat(DEFAULT_ZOOM)
        {
            MinValue = MIN_ZOOM,
            MaxValue = MAX_ZOOM,
        };

        /// <summary>
        /// The time range selected by dragging, which is displayed as a box while dragging.
        /// </summary>
        public readonly Bindable<(double start, double end)?> SelectionBox = new Bindable<(double start, double end)?>();

        /// <summary>
        /// The current zoom in pixels per millisecond.
        /// </summary>
        public float CurrentZoom { get; private set; }

        [Resolved]
        private EditorClock clock { get; set; } = null!;

        public HitsoundTimeline()
        {
            Zoom.Value = lastZoom;
            CurrentZoom = Zoom.Value;
            Zoom.BindValueChanged(z => lastZoom = z.NewValue);
        }

        public double CentreTime => clock.CurrentTime;

        protected override void Update()
        {
            base.Update();

            // interpolating the logarithm keeps the zooming speed the same at all zoom levels.
            CurrentZoom = MathF.Exp((float)Interpolation.DampContinuously(MathF.Log(CurrentZoom), MathF.Log(Zoom.Value), 40, Time.Elapsed));

            if (Precision.AlmostEquals(CurrentZoom, Zoom.Value, 0.0001f))
                CurrentZoom = Zoom.Value;
        }

        /// <summary>
        /// Changes the zoom by a number of steps (positive to zoom in).
        /// </summary>
        public void AdjustZoom(float steps) => Zoom.Value *= MathF.Pow(1.25f, steps);

        public float TimeToX(double time, float width) => (float)(width / 2 + (time - CentreTime) * CurrentZoom);

        public double XToTime(float x, float width) => CentreTime + (x - width / 2) / CurrentZoom;

        /// <summary>
        /// Converts a distance in pixels to a duration.
        /// </summary>
        public double PixelsToDuration(float pixels) => pixels / CurrentZoom;
    }
}
