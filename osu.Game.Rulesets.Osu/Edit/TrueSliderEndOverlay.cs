// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// Marks the positions at which the ends of visible sliders are actually judged (like osu!stable's "true slider ends").
    /// </summary>
    public partial class TrueSliderEndOverlay : CompositeDrawable
    {
        private const float ring_thickness = 3f;

        private readonly Playfield playfield;

        private Bindable<bool> enabled = null!;

        private readonly List<CircularContainer> markers = new List<CircularContainer>();

        public TrueSliderEndOverlay(Playfield playfield)
        {
            this.playfield = playfield;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            enabled = config.GetBindable<bool>(OsuSetting.SlopEditorTrueSliderEnds);
        }

        /// <summary>
        /// The time at which the end of <paramref name="slider"/> is judged, which is slightly before its end time.
        /// </summary>
        public static double GetTrueEndTime(Slider slider) => Math.Max(slider.StartTime + slider.Duration / 2, slider.EndTime + SliderEventGenerator.TAIL_LENIENCY);

        /// <summary>
        /// The position (including stacking) at which the end of <paramref name="slider"/> is judged.
        /// </summary>
        public static Vector2 GetTrueEndPosition(Slider slider)
        {
            double progress = slider.Duration > 0 ? (GetTrueEndTime(slider) - slider.StartTime) / slider.Duration : 1;
            return slider.StackedPosition + slider.CurvePositionAt(Math.Clamp(progress, 0, 1));
        }

        protected override void Update()
        {
            base.Update();

            int count = 0;

            if (enabled.Value)
            {
                foreach (var drawable in playfield.HitObjectContainer.AliveObjects)
                {
                    if (drawable.HitObject is not Slider slider)
                        continue;

                    // positioned every frame as the playfield may move or be resized.
                    Vector2 trueEnd = GetTrueEndPosition(slider);
                    Vector2 centre = ToLocalSpace(playfield.GamefieldToScreenSpace(trueEnd));
                    Vector2 edge = ToLocalSpace(playfield.GamefieldToScreenSpace(trueEnd + new Vector2((float)slider.Radius, 0)));

                    var marker = getMarker(count++);
                    marker.Position = centre;
                    marker.Size = new Vector2(2 * Vector2.Distance(centre, edge));

                    // fades in and out along with the slider, at half its opacity (like osu!stable).
                    marker.Alpha = drawable.Alpha * 0.5f;
                }
            }

            for (int i = count; i < markers.Count; i++)
                markers[i].Alpha = 0;
        }

        private CircularContainer getMarker(int index)
        {
            if (index < markers.Count)
                return markers[index];

            var marker = new CircularContainer
            {
                Origin = Anchor.Centre,
                Masking = true,
                BorderThickness = ring_thickness,
                BorderColour = Color4.White,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Alpha = 0.2f,
                },
            };

            markers.Add(marker);
            AddInternal(marker);

            return marker;
        }
    }
}
