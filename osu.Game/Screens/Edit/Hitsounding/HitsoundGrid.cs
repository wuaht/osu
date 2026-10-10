// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Screens.Edit.Components;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The lanes of the hitsound editor: a lane for each sample, and the volumes below.
    /// The lanes are kept compact and centred vertically, such that the background stays visible above and below them.
    /// The current time is always at the centre.
    /// </summary>
    public partial class HitsoundGrid : CompositeDrawable
    {
        public const float HEADER_WIDTH = 170;

        public const float LANE_HEIGHT = 40;

        private const float volume_height = 88;

        /// <summary>
        /// The height of the volumes while they are hovered, taking space from the lanes if there isn't enough.
        /// </summary>
        private const float expanded_volume_height = 180;

        /// <summary>
        /// The minimum space above and below the lanes. If there isn't enough space, the lanes get smaller.
        /// </summary>
        private const float min_vertical_margin = 10;

        [Resolved]
        private HitsoundEditor hitsoundEditor { get; set; } = null!;

        private Container lanes = null!;
        private Box opaqueBackground = null!;
        private FrostedPanelBackground frostedBackground = null!;

        private readonly BindableBool frosted = new BindableBool();
        private GridContainer grid = null!;
        private HitsoundVolumeArea volumeArea = null!;

        private float currentVolumeHeight = volume_height;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuConfigManager config)
        {
            RelativeSizeAxes = Axes.Both;

            config.BindWith(OsuSetting.SlopHitsoundEditorFrostedLanes, frosted);

            InternalChild = lanes = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.X,
                Masking = true,
                Children = new Drawable[]
                {
                    opaqueBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background5,
                    },
                    frostedBackground = new FrostedPanelBackground(),
                    grid = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Absolute, HEADER_WIDTH),
                            new Dimension(),
                        },
                        RowDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, volume_height),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new HitsoundLaneHeaders(),
                                new HitsoundLaneArea(),
                            },
                            new Drawable[]
                            {
                                new HitsoundHeaderCell
                                {
                                    Child = new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = SlopHitsoundEditorStrings.Volume,
                                        Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                                    },
                                },
                                volumeArea = new HitsoundVolumeArea(),
                            },
                        }
                    },
                    // the playhead, at the centre of the lanes.
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = HEADER_WIDTH },
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 2,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Colour = colourProvider.Highlight1,
                        },
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            frosted.BindValueChanged(f =>
            {
                opaqueBackground.Alpha = f.NewValue ? 0 : 1;
                frostedBackground.Alpha = f.NewValue ? 1 : 0;

                // rounded corners set the lanes apart from the background which shows through them.
                lanes.CornerRadius = f.NewValue ? 8 : 0;
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            float targetVolumeHeight = volumeArea.IsActive ? expanded_volume_height : volume_height;
            float volumeHeight = (float)Interpolation.DampContinuously(currentVolumeHeight, targetVolumeHeight, 30, Time.Elapsed);

            if (Math.Abs(volumeHeight - targetVolumeHeight) < 0.5f)
                volumeHeight = targetVolumeHeight;

            if (volumeHeight != currentVolumeHeight)
            {
                currentVolumeHeight = volumeHeight;

                grid.RowDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute, volumeHeight),
                };
            }

            float wantedHeight = hitsoundEditor.VisibleLanes.Count * LANE_HEIGHT;
            float availableHeight = DrawHeight - min_vertical_margin * 2 - volumeHeight;

            lanes.Height = Math.Max(0, Math.Min(wantedHeight, availableHeight)) + volumeHeight;
        }
    }
}
