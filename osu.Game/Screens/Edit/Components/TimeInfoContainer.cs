// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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

            // laid out to fit the compact bottom bar: the timestamp on top, with progress and BPM left-aligned directly below it.
            Children = new Drawable[]
            {
                new TimestampControl
                {
                    Y = 1,
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Y = -1,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            // fixed width so that the BPM display doesn't shift around as the progress changes (e.g. from "9%" to "10%").
                            Width = 30,
                            AutoSizeAxes = Axes.Y,
                            Child = progress = new OsuSpriteText
                            {
                                Colour = colours.Purple1,
                                Font = OsuFont.Torus.With(size: 11, weight: FontWeight.SemiBold, fixedWidth: true),
                                Spacing = new Vector2(-1, 0),
                            },
                        },
                        bpm = new OsuSpriteText
                        {
                            Colour = colours.Orange1,
                            Font = OsuFont.Torus.With(size: 11, weight: FontWeight.SemiBold, fixedWidth: true),
                            Spacing = new Vector2(-1, 0),
                        },
                    }
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

        private partial class TimestampControl : OsuClickableContainer
        {
            private Container hoverLayer = null!;
            private OsuSpriteText trackTimer = null!;
            private OsuTextBox inputTextBox = null!;

            [Resolved]
            private Editor? editor { get; set; }

            [Resolved]
            private EditorClock editorClock { get; set; } = null!;

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
                        Spacing = new Vector2(-2, 0),
                        Font = OsuFont.Torus.With(size: 20, fixedWidth: true, weight: FontWeight.Light),
                    },
                    inputTextBox = new TimestampTextBox
                    {
                        Position = new Vector2(-2, 0),
                        Width = 100,
                        Height = 20,
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
