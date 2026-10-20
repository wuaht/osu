// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI.Cursor;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Replays.AutoPlus
{
    /// <summary>
    /// Shows several Auto+ cursors which split the objects of the beatmap between them, while the main cursor is hidden.
    /// </summary>
    public partial class AutoPlusTagCursorContainer : CompositeDrawable
    {
        private const double main_cursor_fade_duration = 300;

        private readonly Playfield playfield;
        private readonly IBeatmap beatmap;
        private readonly IReadOnlyList<Mod> mods;

        [Resolved(canBeNull: true)]
        private SkinManager? skinManager { get; set; }

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        /// <summary>
        /// The number of shown tag cursors.
        /// </summary>
        public int CursorCount => InternalChildren.Count;

        public AutoPlusTagCursorContainer(Playfield playfield, IBeatmap beatmap, IReadOnlyList<Mod> mods)
        {
            this.playfield = playfield;
            this.beatmap = beatmap;
            this.mods = mods;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            var colourSource = config.Get<AutoPlusTagColourSource>(OsuSetting.SlopAutoPlusTagColours);
            var splitMode = config.Get<AutoPlusTagSplitMode>(OsuSetting.SlopAutoPlusTagSplit);

            IReadOnlyList<Color4>? skinColours = skinManager?.CurrentSkin.Value.GetConfig<GlobalSkinColours, IReadOnlyList<Color4>>(GlobalSkinColours.ComboColours)?.Value;
            IReadOnlyList<Color4>? mapColours = workingBeatmap.Value?.Skin?.GetConfig<GlobalSkinColours, IReadOnlyList<Color4>>(GlobalSkinColours.ComboColours)?.Value;
            IReadOnlyList<Color4>? gameplayColours = skin.GetConfig<GlobalSkinColours, IReadOnlyList<Color4>>(GlobalSkinColours.ComboColours)?.Value;

            if (mapColours == null || mapColours.Count == 0)
                mapColours = skinColours;

            int configuredCount = config.Get<int>(OsuSetting.SlopAutoPlusTagCount);
            int count = configuredCount > 0 ? configuredCount : (colourSource == AutoPlusTagColourSource.SkinColours ? skinColours?.Count : mapColours?.Count) ?? 1;
            count = Math.Max(count, 1);

            var objects = beatmap.HitObjects.OfType<OsuHitObject>().ToList();
            var subsets = split(objects, count, splitMode, Math.Max(gameplayColours?.Count ?? 1, 1));

            // The preempt of the hit objects already includes the difficulty adjustments of mods.
            double preempt = objects.FirstOrDefault()?.TimePreempt ?? OsuHitObject.PREEMPT_MAX;

            Color4[] colours = getColours(colourSource, count, mapColours, skinColours);

            for (int i = 0; i < count; i++)
            {
                if (subsets[i].All(h => h is Spinner))
                    continue;

                var frames = new OsuAutoPlusGenerator(beatmap, mods, subsets[i]).Generate().Frames.Cast<OsuReplayFrame>().ToList();

                if (frames.Count == 0)
                    continue;

                AddInternal(new TagCursor(playfield, frames, colours[i], preempt));
            }
        }

        private static List<OsuHitObject>[] split(List<OsuHitObject> objects, int count, AutoPlusTagSplitMode mode, int colourCount)
        {
            var subsets = Enumerable.Range(0, count).Select(_ => new List<OsuHitObject>()).ToArray();

            int index = -1;
            int[] nextIndexOfColour = Enumerable.Range(0, colourCount).Select(i => i % count).ToArray();

            for (int i = 0; i < objects.Count; i++)
            {
                var obj = objects[i];

                if (obj is Spinner)
                {
                    foreach (var subset in subsets)
                        subset.Add(obj);

                    continue;
                }

                switch (mode)
                {
                    case AutoPlusTagSplitMode.Combo:
                        if (obj.NewCombo || index < 0)
                            index = (index + 1) % count;
                        break;

                    case AutoPlusTagSplitMode.Colour:
                        int colourIndex = obj.ComboIndexWithOffsets % colourCount;
                        index = nextIndexOfColour[colourIndex];

                        nextIndexOfColour[colourIndex] += colourCount;
                        if (nextIndexOfColour[colourIndex] >= count)
                            nextIndexOfColour[colourIndex] = colourIndex % count;
                        break;

                    default:
                        index = (index + 1) % count;
                        break;
                }

                subsets[index].Add(obj);
            }

            return subsets;
        }

        private static Color4[] getColours(AutoPlusTagColourSource source, int count, IReadOnlyList<Color4>? mapColours, IReadOnlyList<Color4>? skinColours)
        {
            IReadOnlyList<Color4>? palette = source switch
            {
                AutoPlusTagColourSource.MapColours => mapColours,
                AutoPlusTagColourSource.SkinColours => skinColours,
                _ => null,
            };

            var colours = new Color4[count];

            for (int i = 0; i < count; i++)
            {
                if (palette == null || palette.Count == 0)
                    colours[i] = Colour4.FromHSL((float)i / count, 0.95f, 0.55f);
                else
                    colours[i] = palette[i % palette.Count];
            }

            return colours;
        }

        protected override void Update()
        {
            base.Update();

            // Only the tag cursors are shown, the main cursor just plays the beatmap.
            var mainCursor = playfield.Cursor;

            if (mainCursor != null && mainCursor.Alpha > 0)
                mainCursor.Alpha = (float)Math.Max(0, mainCursor.Alpha - Time.Elapsed / main_cursor_fade_duration);
        }

        private partial class TagCursor : CompositeDrawable
        {
            /// <summary>
            /// The time after the last press until the cursor is hidden.
            /// </summary>
            private const double hide_delay = 1240;

            /// <summary>
            /// Presses with a larger gap in between split the cursor into separate active ranges.
            /// </summary>
            private const double range_split_gap = 5000;

            private readonly Playfield playfield;
            private readonly List<OsuReplayFrame> frames;
            private readonly Color4 colour;
            private readonly List<(double start, double end)> activeRanges = new List<(double start, double end)>();

            private OsuCursor cursor = null!;
            private SdfCursorTrail trail = null!;

            private int frameIndex;
            private bool visible = true;
            private readonly double preempt;

            public TagCursor(Playfield playfield, List<OsuReplayFrame> frames, Color4 colour, double preempt)
            {
                this.playfield = playfield;
                this.preempt = preempt;
                this.frames = frames;
                this.colour = colour;

                RelativeSizeAxes = Axes.Both;

                // keeps updating while faded out, so the cursor can fade back in once its next range starts.
                AlwaysPresent = true;

                double start = frames[0].Time;
                double end = frames[0].Time;

                for (int i = 1; i < frames.Count; i++)
                {
                    if (frames[i].Actions.Count == 0)
                        continue;

                    if (frames[i].Time - end > range_split_gap)
                    {
                        activeRanges.Add((start, end));
                        start = frames[i].Time;
                    }

                    end = frames[i].Time;
                }

                activeRanges.Add((start, end));
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    trail = new SdfCursorTrail
                    {
                        FollowMouse = false,
                        Colour = colour,
                    },
                    cursor = new OsuCursor
                    {
                        Colour = colour,
                    },
                };
            }

            protected override void Update()
            {
                base.Update();

                double time = playfield.Time.Current;

                bool active = activeRanges.Any(r => time >= r.start - preempt - 1000 && time <= r.end + hide_delay);

                if (time < frames[0].Time)
                    active = false;

                if (active != visible)
                {
                    visible = active;
                    this.FadeTo(active ? 1 : 0, active ? 100 : 300);
                }

                while (frameIndex > 0 && frames[frameIndex].Time > time)
                    frameIndex--;

                while (frameIndex < frames.Count - 1 && frames[frameIndex + 1].Time <= time)
                    frameIndex++;

                Vector2 screenSpacePosition = playfield.ToScreenSpace(frames[frameIndex].Position);

                cursor.Position = ToLocalSpace(screenSpacePosition);

                // one position per frame, like the main cursor.
                trail.AddPosition(screenSpacePosition);
            }
        }
    }
}
