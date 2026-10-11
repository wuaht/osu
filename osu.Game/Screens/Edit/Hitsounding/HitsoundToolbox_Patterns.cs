// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osuTK;

namespace osu.Game.Screens.Edit.Hitsounding
{
    public partial class HitsoundToolbox
    {
        /// <summary>
        /// Creates hitsound patterns from the selection, and lists the patterns to place, rename and delete them.
        /// </summary>
        private partial class PatternTools : CompositeDrawable
        {
            [Resolved]
            private HitsoundScreen screen { get; set; } = null!;

            [Resolved]
            private HitsoundPatterns patterns { get; set; } = null!;

            [Resolved]
            private EditorBeatmap editorBeatmap { get; set; } = null!;

            private FillFlowContainer patternFlow = null!;

            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(8),
                    Children = new Drawable[]
                    {
                        createButton(SlopHitsoundEditorStrings.CreatePattern, SlopHitsoundEditorStrings.CreatePatternTooltip, () => screen.CreatePatternFromSelection()),
                        patternFlow = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(6),
                        },
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                patterns.Changed += onPatternsChanged;
                updatePatterns();
            }

            private void onPatternsChanged() => Scheduler.AddOnce(updatePatterns);

            private void updatePatterns()
            {
                patternFlow.Clear();

                if (patterns.Patterns.Count == 0)
                {
                    patternFlow.Add(new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 13))
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Text = SlopHitsoundEditorStrings.NoPatterns,
                    });
                    return;
                }

                foreach (var pattern in patterns.Patterns)
                    patternFlow.Add(new PatternRow(pattern, getBeats(pattern)));
            }

            private string getBeats(HitsoundPattern pattern)
            {
                var instance = patterns.GetOccurrences(pattern).FirstOrDefault();
                double beatLength = editorBeatmap.ControlPointInfo.TimingPointAt(instance?.StartTime ?? 0).BeatLength;

                return (pattern.Length / beatLength).ToString(@"0.##", CultureInfo.CurrentCulture);
            }

            protected override void Dispose(bool isDisposing)
            {
                if (patterns.IsNotNull())
                    patterns.Changed -= onPatternsChanged;

                base.Dispose(isDisposing);
            }
        }

        private partial class PatternRow : CompositeDrawable
        {
            private readonly HitsoundPattern pattern;
            private readonly string beats;

            [Resolved]
            private HitsoundScreen screen { get; set; } = null!;

            [Resolved]
            private HitsoundPatterns patterns { get; set; } = null!;

            public PatternRow(HitsoundPattern pattern, string beats)
            {
                this.pattern = pattern;
                this.beats = beats;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                OsuTextBox nameTextBox;

                InternalChild = new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, 6),
                        new Dimension(GridSizeMode.Absolute, 6),
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 4),
                        new Dimension(GridSizeMode.Absolute, 60),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                CornerRadius = 3,
                                Child = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = pattern.Colour,
                                },
                            },
                            Empty(),
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(2),
                                Children = new Drawable[]
                                {
                                    nameTextBox = new OsuTextBox
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 30,
                                        Text = pattern.Name,
                                        CommitOnFocusLost = true,
                                    },
                                    new OsuSpriteText
                                    {
                                        Text = SlopHitsoundEditorStrings.PatternUsage(patterns.GetOccurrences(pattern).Count(), beats),
                                        Font = OsuFont.Default.With(size: 11),
                                        Alpha = 0.7f,
                                    },
                                }
                            },
                            Empty(),
                            createButton(SlopHitsoundEditorStrings.PlacePattern, SlopHitsoundEditorStrings.PlacePatternTooltip, () => screen.PlacePattern(pattern)).With(b =>
                            {
                                b.Anchor = Anchor.TopLeft;
                                b.Origin = Anchor.TopLeft;
                            }),
                            new IconButton
                            {
                                Size = new Vector2(30),
                                IconScale = new Vector2(0.7f),
                                Icon = FontAwesome.Solid.Trash,
                                TooltipText = SlopHitsoundEditorStrings.DeletePatternTooltip,
                                Action = () => patterns.Delete(pattern),
                            },
                        },
                    },
                };

                nameTextBox.OnCommit += (textBox, _) =>
                {
                    patterns.Rename(pattern, textBox.Text);
                    // an empty name isn't accepted, so the name is restored.
                    textBox.Text = pattern.Name;
                };
            }
        }
    }
}
