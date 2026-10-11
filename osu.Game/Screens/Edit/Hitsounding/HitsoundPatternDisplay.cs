// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
        private readonly Dictionary<HitsoundPatternInstance, InstanceClip> clipsByInstance = new Dictionary<HitsoundPatternInstance, InstanceClip>();

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
            clipsByInstance.Clear();

            foreach (var instance in patterns.Instances)
            {
                if (patterns.GetPattern(instance.PatternID) is not HitsoundPattern pattern)
                    continue;

                var clip = new InstanceClip(pattern, instance);
                clips.Add(clip);
                clipsByInstance[instance] = clip;
            }
        }

        protected override void Update()
        {
            base.Update();

            foreach (var (instance, clip) in clipsByInstance)
            {
                float start = TimeToX(instance.StartTime);
                float end = TimeToX(patterns.GetEndTime(instance));

                // clips far outside of the visible area aren't drawn.
                clip.Alpha = end < -20 || start > DrawWidth + 20 ? 0 : 1;
                clip.X = start;
                clip.Width = end - start;
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

            private readonly HitsoundPattern pattern;
            private readonly HitsoundPatternInstance instance;

            [Resolved]
            private HitsoundScreen screen { get; set; } = null!;

            [Resolved]
            private HitsoundPatterns patterns { get; set; } = null!;

            [Resolved]
            private EditorClock clock { get; set; } = null!;

            private Box fill = null!;

            public InstanceClip(HitsoundPattern pattern, HitsoundPatternInstance instance)
            {
                this.pattern = pattern;
                this.instance = instance;

                RelativeSizeAxes = Axes.Y;
            }

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

            public LocalisableString TooltipText => SlopHitsoundEditorStrings.PatternInstanceTooltip(pattern.Name);

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

            protected override bool OnClick(ClickEvent e)
            {
                screen.SelectPatternInstance(instance);
                return true;
            }

            protected override bool OnDoubleClick(DoubleClickEvent e)
            {
                clock.SeekSmoothlyTo(instance.StartTime);
                return true;
            }

            public MenuItem[] ContextMenuItems => new MenuItem[]
            {
                new OsuMenuItem(SlopHitsoundEditorStrings.SelectPatternHitsounds, MenuItemType.Standard, () => screen.SelectPatternInstance(instance)),
                new OsuMenuItem(SlopHitsoundEditorStrings.PlacePatternAgain, MenuItemType.Standard, () => screen.PlacePattern(pattern)),
                new OsuMenuItem(SlopHitsoundEditorStrings.RemovePatternInstance, MenuItemType.Destructive, () => patterns.RemoveInstance(instance)),
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

        private readonly Dictionary<HitsoundPatternInstance, Container> regions = new Dictionary<HitsoundPatternInstance, Container>();

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

            foreach (var instance in patterns.Instances)
            {
                if (patterns.GetPattern(instance.PatternID) is not HitsoundPattern pattern)
                    continue;

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
                regions[instance] = region;
            }
        }

        protected override void Update()
        {
            base.Update();

            foreach (var (instance, region) in regions)
            {
                float start = timeline.TimeToX(instance.StartTime, DrawWidth);
                float end = timeline.TimeToX(patterns.GetEndTime(instance), DrawWidth);

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
