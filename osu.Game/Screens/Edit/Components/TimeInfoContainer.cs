// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Edit.Components
{
    public partial class TimeInfoContainer : BottomBarContainer
    {
        /// <summary>
        /// The horizontal space between the timestamp and the progress / BPM display.
        /// </summary>
        private const float stats_spacing = 8;

        /// <summary>
        /// The distance from the right edge of this container to the visible start of the summary timeline's centre line (its padding minus the radius of the end circle).
        /// The contents are centred between the left edge and that point.
        /// </summary>
        private const float summary_timeline_line_offset = 2.5f;

        private TimestampControl timestamp = null!;
        private FillFlowContainer stats = null!;
        private OsuSpriteText bpm = null!;
        private OsuSpriteText progress = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, OverlayColourProvider colourProvider)
        {
            Background.Colour = EditorPanelStyle.PanelBackground;

            // the contents are centred manually (see UpdateAfterChildren()).
            ContentPadding = new MarginPadding();

            // laid out like osu!stable: the timestamp, with progress and BPM left-aligned below each other next to it.
            Children = new Drawable[]
            {
                stats = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Children = new Drawable[]
                    {
                        progress = new OsuSpriteText
                        {
                            Colour = colours.Purple1,
                            Font = OsuFont.Torus.With(size: 12, weight: FontWeight.SemiBold, fixedWidth: true),
                            Spacing = new Vector2(-1, 0),
                        },
                        bpm = new OsuSpriteText
                        {
                            Colour = colours.Orange1,
                            Font = OsuFont.Torus.With(size: 12, weight: FontWeight.SemiBold, fixedWidth: true),
                            Spacing = new Vector2(-1, 0),
                        },
                    }
                },
                // after the progress and BPM display, so that the text box for entering a timestamp is displayed above it.
                timestamp = new TimestampControl
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
            };
        }

        private double? lastBPM;
        private double? lastProgress;

        protected override void Update()
        {
            base.Update();

            double newBPM = editorBeatmap.ControlPointInfo.TimingPointAt(editorClock.CurrentTime).BPM;
            double newProgress = (int)(editorClock.CurrentTime / editorClock.TrackLength * 100);

            if (lastBPM != newBPM)
            {
                lastBPM = newBPM;
                bpm.Text = @$"{newBPM:0} BPM";
            }

            if (lastProgress != newProgress)
            {
                lastProgress = newProgress;
                progress.Text = @$"{newProgress:0}%";
            }
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            // based on the width of the displayed time rather than the whole control, so that the text box for entering a timestamp doesn't move the display.
            float contentWidth = timestamp.TimeWidth + stats_spacing + stats.DrawWidth;

            timestamp.X = Math.Max(0, (DrawWidth + summary_timeline_line_offset - contentWidth) / 2);
            stats.X = timestamp.X + timestamp.TimeWidth + stats_spacing;
        }

        private partial class TimestampControl : OsuClickableContainer
        {
            private Container hoverLayer = null!;
            private OsuSpriteText trackTimer = null!;
            private OsuTextBox inputTextBox = null!;

            [Resolved]
            private Editor? editor { get; set; }

            [Resolved]
            private EditorClock editorClock { get; set; } = null!;

            /// <summary>
            /// The width of the displayed time.
            /// </summary>
            public float TimeWidth => trackTimer.DrawWidth;

            public TimestampControl()
                : base(HoverSampleSet.Button)
            {
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                AutoSizeAxes = Axes.Both;

                AddRangeInternal(new Drawable[]
                {
                    hoverLayer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding
                        {
                            Horizontal = -2
                        },
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            CornerRadius = 5,
                            Masking = true,
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.Both, },
                            }
                        },
                        Alpha = 0,
                    },
                    trackTimer = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        // about as tall as the progress and BPM display next to it.
                        Spacing = new Vector2(-2.6f, 0),
                        Font = OsuFont.Torus.With(size: 26, fixedWidth: true, weight: FontWeight.Light),
                    },
                    inputTextBox = new TimestampTextBox
                    {
                        Position = new Vector2(-2, 0),
                        Width = 130,
                        Height = 26,
                        Alpha = 0,
                        CommitOnFocusLost = true,
                    },
                });

                Action = () =>
                {
                    trackTimer.Alpha = 0;
                    inputTextBox.Alpha = 1;
                    inputTextBox.Text = editorClock.CurrentTime.ToEditorFormattedString();
                    Schedule(() =>
                    {
                        GetContainingFocusManager()!.ChangeFocus(inputTextBox);
                        inputTextBox.SelectAll();
                    });
                };

                inputTextBox.Current.BindValueChanged(val => editor?.HandleTimestamp(val.NewValue.Trim()));

                inputTextBox.OnCommit += (_, _) =>
                {
                    trackTimer.Alpha = 1;
                    inputTextBox.Alpha = 0;
                };
            }

            private double? lastTime;
            private bool showingHoverLayer;

            protected override void Update()
            {
                base.Update();

                if (lastTime != editorClock.CurrentTime)
                {
                    lastTime = editorClock.CurrentTime;
                    trackTimer.Text = editorClock.CurrentTime.ToEditorFormattedString();
                }

                bool shouldShowHoverLayer = IsHovered && inputTextBox.Alpha == 0;

                if (shouldShowHoverLayer != showingHoverLayer)
                {
                    hoverLayer.FadeTo(shouldShowHoverLayer ? 0.2f : 0, 400, Easing.OutQuint);
                    showingHoverLayer = shouldShowHoverLayer;
                }
            }

            private partial class TimestampTextBox : OsuTextBox
            {
                public TimestampTextBox()
                {
                    TextContainer.Height = 0.8f;
                }
            }
        }
    }
}
