// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The top of the hitsound editor: the view controls, the hit objects of the beatmap, and the beat snap control.
    /// </summary>
    public partial class HitsoundTopBar : CompositeDrawable
    {
        public const float HEIGHT = 84;

        /// <summary>
        /// The width of the beat snap control on the right.
        /// Kept the same as the width of the toolbox below, such that the hit objects line up with the lanes.
        /// </summary>
        public float RightWidth { get; set; } = HitsoundToolbox.CONTRACTED_WIDTH;

        [Resolved]
        private HitsoundTimeline timeline { get; set; } = null!;

        [Resolved]
        private HitsoundEditor hitsoundEditor { get; set; } = null!;

        private readonly BindableBool hideUnusedLanes = new BindableBool();

        private Container stripContainer = null!;
        private Container beatSnapContainer = null!;
        private IconButton hideUnusedLanesButton = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = EditorPanelStyle.PanelBackground,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = HitsoundGrid.HEADER_WIDTH,
                    Child = new HitsoundHeaderCell
                    {
                        Child = new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(2, 0),
                            Children = new Drawable[]
                            {
                                createButton(FontAwesome.Solid.SearchMinus, SlopHitsoundEditorStrings.ZoomOut, () => timeline.AdjustZoom(-1)),
                                createButton(FontAwesome.Solid.SearchPlus, SlopHitsoundEditorStrings.ZoomIn, () => timeline.AdjustZoom(1)),
                                hideUnusedLanesButton = createButton(FontAwesome.Solid.EyeSlash, default, () => hideUnusedLanes.Toggle()),
                                new HelpIcon(),
                                new SoundGuideIcon(),
                            }
                        },
                    },
                },
                stripContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new HitsoundObjectStrip(),
                        // the playhead, at the same position as in the lanes below.
                        new Box
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 2,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Colour = colourProvider.Highlight1,
                        },
                    }
                },
                beatSnapContainer = new Container
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    RelativeSizeAxes = Axes.Y,
                    Child = new BeatDivisorControl { RelativeSizeAxes = Axes.Both },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            hideUnusedLanes.BindTo(hitsoundEditor.HideUnusedLanes);
            hideUnusedLanes.BindValueChanged(h =>
            {
                hideUnusedLanesButton.Icon = h.NewValue ? FontAwesome.Solid.Eye : FontAwesome.Solid.EyeSlash;
                hideUnusedLanesButton.TooltipText = h.NewValue ? SlopHitsoundEditorStrings.ShowAllLanes : SlopHitsoundEditorStrings.HideUnusedLanes;
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            stripContainer.Padding = new MarginPadding { Left = HitsoundGrid.HEADER_WIDTH, Right = RightWidth };
            beatSnapContainer.Width = RightWidth;
        }

        private static IconButton createButton(IconUsage icon, LocalisableString tooltip, System.Action action) => new IconButton
        {
            Icon = icon,
            Size = new Vector2(26),
            IconScale = new Vector2(0.8f),
            TooltipText = tooltip,
            Action = action,
        };

        /// <summary>
        /// Explains something in its tooltip.
        /// </summary>
        private abstract partial class InfoIcon : CompositeDrawable, IHasHitsoundTooltip
        {
            public abstract LocalisableString TooltipText { get; }

            protected InfoIcon(IconUsage icon)
            {
                Size = new Vector2(26);

                InternalChild = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(14),
                    Icon = icon,
                };
            }
        }

        /// <summary>
        /// Explains the mouse and keyboard controls in its tooltip.
        /// </summary>
        private partial class HelpIcon : InfoIcon
        {
            public override LocalisableString TooltipText => SlopHitsoundEditorStrings.ControlsDescription;

            public HelpIcon()
                : base(FontAwesome.Solid.QuestionCircle)
            {
            }
        }

        /// <summary>
        /// Lists which hitsounds are commonly used for which sounds of a song in its tooltip.
        /// </summary>
        private partial class SoundGuideIcon : InfoIcon
        {
            public override LocalisableString TooltipText => HitsoundSoundGuide.Describe();

            public SoundGuideIcon()
                : base(FontAwesome.Solid.Drum)
            {
            }
        }
    }
}
