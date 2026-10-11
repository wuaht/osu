// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osuTK.Input;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The row above the lanes which shows where hitsound patterns are used, as clips like in the playlist of FL Studio.
    /// </summary>
    public partial class HitsoundPatternStrip : HitsoundTimelinePart
    {
        public const float HEIGHT = 26;

        [Resolved]
        private HitsoundPatterns patterns { get; set; } = null!;

        private readonly Container<InstanceClip> clips;
        private readonly List<(HitsoundPatternOccurrence occurrence, InstanceClip clip)> clipsByOccurrence = new List<(HitsoundPatternOccurrence, InstanceClip)>();

        public HitsoundPatternStrip()
        {
            InternalChildren = new Drawable[]
            {
                new HitsoundTicks(0.3f),
                clips = new Container<InstanceClip>
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Vertical = 3 },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            patterns.Changed += onPatternsChanged;
            onPatternsChanged();
        }

        private void onPatternsChanged() => Scheduler.AddOnce(updateClips);

        private void updateClips()
        {
            clips.Clear();
            clipsByOccurrence.Clear();

            // sub-patterns are added last, such that they are drawn on top of (and receive input before) the patterns containing them.
            foreach (var occurrence in patterns.Occurrences.OrderBy(o => o.Depth))
            {
                var clip = new InstanceClip(occurrence);
                clips.Add(clip);
                clipsByOccurrence.Add((occurrence, clip));
            }
        }

        protected override void Update()
        {
            base.Update();

            foreach (var (occurrence, clip) in clipsByOccurrence)
            {
                float start = TimeToX(occurrence.StartTime + clip.DragOffset);
                float end = TimeToX(occurrence.EndTime + clip.DragOffset);

                // clips far outside of the visible area aren't drawn.
                clip.Alpha = end < -20 || start > DrawWidth + 20 ? 0 : 1;
                clip.X = start;
                clip.Width = end - start;
                clip.Y = clip.Inset;
                clip.Height = Math.Max(4, clips.ChildSize.Y - clip.Inset * 2);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (patterns.IsNotNull())
                patterns.Changed -= onPatternsChanged;

            base.Dispose(isDisposing);
        }

        private partial class InstanceClip : CompositeDrawable, IHasContextMenu, IHasHitsoundTooltip
        {
            private const float idle_alpha = 0.7f;

            /// <summary>
            /// How far sub-patterns are inset from the top and bottom for each level they are nested in.
            /// </summary>
            private const float inset_per_depth = 4;

            private readonly HitsoundPatternOccurrence occurrence;
            private HitsoundPattern pattern => occurrence.Pattern;

            [Resolved]
            private HitsoundScreen screen { get; set; } = null!;

            [Resolved]
            private HitsoundPatterns patterns { get; set; } = null!;

            [Resolved]
            private EditorClock clock { get; set; } = null!;

            [Resolved]
            private HitsoundTimeline timeline { get; set; } = null!;

            [Resolved]
            private EditorBeatmap editorBeatmap { get; set; } = null!;

            private Box fill = null!;

            /// <summary>
            /// How far the clip is being dragged from its place, snapped to the beat divisor.
            /// </summary>
            public double DragOffset { get; private set; }

            private float dragDistance;

            public InstanceClip(HitsoundPatternOccurrence occurrence)
            {
                this.occurrence = occurrence;

                // sub-patterns are drawn smaller, inside the clips of the patterns containing them.
                Inset = Math.Min(occurrence.Depth * inset_per_depth, HEIGHT / 2 - 8);
            }

            /// <summary>
            /// The distance from the top and bottom of the strip.
            /// </summary>
            public readonly float Inset;

            [BackgroundDependencyLoader]
            private void load()
            {
                Colour4 colour = pattern.Colour;

                Masking = true;
                CornerRadius = 4;
                BorderThickness = 1.5f;
                BorderColour = colour.Lighten(0.4f);
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Colour = Colour4.Black.Opacity(0.25f),
                    Radius = 3,
                };

                InternalChildren = new Drawable[]
                {
                    fill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(colour, colour.Darken(0.4f)),
                        Alpha = idle_alpha,
                    },
                    // an accent at the start, like the clips of FL Studio.
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 3,
                        Colour = colour.Lighten(0.6f),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 8,
                        Text = pattern.Name,
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                        Shadow = true,
                        ShadowColour = Colour4.Black.Opacity(0.5f),
                    },
                };
            }

            public LocalisableString TooltipText => SlopHitsoundEditorStrings.PatternInstanceTooltip(occurrence.Parent != null
                ? SlopHitsoundEditorStrings.SubPatternName(pattern.Name, occurrence.Parent.Name).ToString()
                : pattern.Name);

            protected override bool OnHover(HoverEvent e)
            {
                fill.FadeTo(1, 100, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                fill.FadeTo(idle_alpha, 200, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            protected override bool OnDragStart(DragStartEvent e)
            {
                if (e.Button != MouseButton.Left)
                    return false;

                dragDistance = 0;
                DragOffset = 0;
                return true;
            }

            protected override void OnDrag(DragEvent e)
            {
                // like clips in FL Studio, dragging moves the pattern (with its hitsounds) to another place.
                dragDistance += e.Delta.X;

                double target = editorBeatmap.SnapTime(occurrence.StartTime + timeline.PixelsToDuration(dragDistance), null);
                DragOffset = target - occurrence.StartTime;
            }

            protected override void OnDragEnd(DragEndEvent e)
            {
                double offset = DragOffset;
                DragOffset = 0;

                if (Math.Abs(offset) >= 1)
                    screen.MovePatternOccurrence(occurrence, occurrence.StartTime + offset);
            }

            protected override bool OnClick(ClickEvent e)
            {
                screen.SelectPatternOccurrence(occurrence);
                return true;
            }

            protected override bool OnDoubleClick(DoubleClickEvent e)
            {
                clock.SeekSmoothlyTo(occurrence.StartTime);
                return true;
            }

            public MenuItem[] ContextMenuItems => new MenuItem[]
            {
                new OsuMenuItem(SlopHitsoundEditorStrings.SelectPatternHitsounds, MenuItemType.Standard, () => screen.SelectPatternOccurrence(occurrence)),
                new OsuMenuItem(SlopHitsoundEditorStrings.PlacePatternAgain, MenuItemType.Standard, () => screen.PlacePattern(pattern)),
                new OsuMenuItem(SlopHitsoundEditorStrings.MakePatternUnique, MenuItemType.Standard, () => screen.MakePatternUnique(occurrence)),
                new OsuMenuItem(occurrence.Parent != null ? SlopHitsoundEditorStrings.RemoveSubPattern : SlopHitsoundEditorStrings.RemovePatternInstance, MenuItemType.Destructive,
                    () => screen.RemovePatternOccurrence(occurrence, keepHitsounds: false)),
                new OsuMenuItem(SlopHitsoundEditorStrings.UnlinkPatternInstance, MenuItemType.Destructive, () => screen.RemovePatternOccurrence(occurrence, keepHitsounds: true)),
                new OsuMenuItem(SlopHitsoundEditorStrings.DeletePattern, MenuItemType.Destructive, () => patterns.Delete(pattern)),
            };
        }
    }

    /// <summary>
    /// Tints the parts of the lanes where hitsound patterns are used in their colour, with lines at their start and end.
    /// </summary>
    public partial class HitsoundPatternRegions : CompositeDrawable
    {
        [Resolved]
        private HitsoundTimeline timeline { get; set; } = null!;

        [Resolved]
        private HitsoundPatterns patterns { get; set; } = null!;

        private readonly List<(HitsoundPatternOccurrence occurrence, Container region)> regions = new List<(HitsoundPatternOccurrence, Container)>();

        public HitsoundPatternRegions()
        {
            RelativeSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            patterns.Changed += onPatternsChanged;
            onPatternsChanged();
        }

        private void onPatternsChanged() => Scheduler.AddOnce(updateRegions);

        private void updateRegions()
        {
            ClearInternal();
            regions.Clear();

            foreach (var occurrence in patterns.Occurrences)
            {
                var pattern = occurrence.Pattern;

                var region = new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = pattern.Colour,
                            Alpha = 0.07f,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 2,
                            Colour = pattern.Colour,
                            Alpha = 0.6f,
                        },
                        new Box
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            RelativeSizeAxes = Axes.Y,
                            Width = 1,
                            Colour = pattern.Colour,
                            Alpha = 0.35f,
                        },
                    }
                };

                AddInternal(region);
                regions.Add((occurrence, region));
            }
        }

        protected override void Update()
        {
            base.Update();

            foreach (var (occurrence, region) in regions)
            {
                float start = timeline.TimeToX(occurrence.StartTime, DrawWidth);
                float end = timeline.TimeToX(occurrence.EndTime, DrawWidth);

                region.Alpha = end < 0 || start > DrawWidth ? 0 : 1;
                region.X = start;
                region.Width = end - start;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (patterns.IsNotNull())
                patterns.Changed -= onPatternsChanged;

            base.Dispose(isDisposing);
        }
    }
}
