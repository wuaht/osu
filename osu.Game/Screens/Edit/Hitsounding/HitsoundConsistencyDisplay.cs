// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osuTK;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Shows for each other difficulty of the beatmap set whether its hitsounds are the same as those of this difficulty (see <see cref="HitsoundConsistency"/>),
    /// and otherwise where they differ.
    /// </summary>
    public partial class HitsoundConsistencyDisplay : CompositeDrawable
    {
        /// <summary>
        /// The number of differences listed per difficulty.
        /// </summary>
        private const int max_listed_differences = 5;

        [Resolved]
        private HitsoundEditor hitsoundEditor { get; set; } = null!;

        [Resolved]
        private HitsoundScreen screen { get; set; } = null!;

        [Resolved]
        private EditorClock clock { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private FillFlowContainer flow = null!;

        /// <summary>
        /// The saved hitsounds of the other difficulties, once they are loaded.
        /// </summary>
        private List<(BeatmapInfo difficulty, IReadOnlyList<HitsoundColumnState>? states)>? others;

        /// <summary>
        /// Whether the other difficulties have been loaded and are shown.
        /// </summary>
        public bool HasLoadedDifficulties => others != null;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(2),
            };

            addText(SlopHitsoundEditorStrings.ConsistencyHint, 11, Colour4.Gray);
            addText(SlopHitsoundEditorStrings.Loading, 12, Colour4.White);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var difficulties = screen.GetOtherDifficulties();

            // the other difficulties don't change while this one is edited, so they are only loaded once.
            Task.Run(() => difficulties.Select(d =>
            {
                try
                {
                    return (d, (IReadOnlyList<HitsoundColumnState>?)screen.LoadHitsounds(d).Columns.Select(c => c.GetState()).ToList());
                }
                catch (Exception e)
                {
                    Logger.Error(e, $@"Failed to load hitsounds of {d.GetDisplayTitle()}");
                    return (d, null);
                }
            }).ToList()).ContinueWith(t => Schedule(() =>
            {
                if (!t.IsCompletedSuccessfully)
                    return;

                others = t.GetResultSafely();
                updateDisplay();
            }));

            hitsoundEditor.MapChanged += scheduleUpdate;
        }

        private void scheduleUpdate() => Scheduler.AddOnce(updateDisplay);

        private void updateDisplay()
        {
            if (others == null)
                return;

            flow.Clear();
            addText(SlopHitsoundEditorStrings.ConsistencyHint, 11, Colour4.Gray);

            if (others.Count == 0)
            {
                addText(SlopHitsoundEditorStrings.NoOtherDifficulties, 12, Colour4.White);
                return;
            }

            var reference = hitsoundEditor.Map.Columns.Select(c => c.GetState()).ToList();

            foreach (var (difficulty, states) in others)
            {
                flow.Add(new ClickableText(difficulty.DifficultyName, 13, FontWeight.Bold, () =>
                {
                    // stay at the consistency of the difficulties after switching.
                    HitsoundToolbox.ScrollToConsistencyAfterSwitch();
                    screen.SwitchToDifficulty(difficulty);
                })
                {
                    TooltipText = SlopHitsoundEditorStrings.SwitchToDifficulty,
                });

                if (states == null)
                {
                    addText(SlopHitsoundEditorStrings.ImportFailed(difficulty.DifficultyName), 12, colours.Red1);
                    continue;
                }

                if (!HitsoundPreferences.ReceivesHitsounds(config, difficulty))
                {
                    addText(SlopHitsoundEditorStrings.DoesntReceiveHitsounds, 12, Colour4.Gray);
                    continue;
                }

                var differences = HitsoundConsistency.Compare(reference, states);

                if (differences.Count == 0)
                {
                    addText(SlopHitsoundEditorStrings.SameHitsounds, 12, colours.Green1);
                    continue;
                }

                addText(SlopHitsoundEditorStrings.DifferentHitsounds(differences.Count), 12, colours.Orange1);

                foreach (var difference in differences.Take(max_listed_differences))
                {
                    flow.Add(new ClickableText($@"{difference.Time.ToEditorFormattedString()}  {string.Join("; ", difference.Details)}", 11, FontWeight.Regular,
                        () => clock.SeekSmoothlyTo(difference.Time))
                    {
                        Padding = new MarginPadding { Left = 5 },
                    });
                }

                if (differences.Count > max_listed_differences)
                    addText(SlopHitsoundEditorStrings.AndMore(differences.Count - max_listed_differences), 11, Colour4.Gray);
            }
        }

        private void addText(LocalisableString text, float size, Colour4 colour, FontWeight weight = FontWeight.Regular) => flow.Add(new OsuTextFlowContainer(t =>
        {
            t.Font = OsuFont.Default.With(size: size, weight: weight);
            t.Colour = colour;
        })
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Text = text,
        });

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (hitsoundEditor.IsNotNull())
                hitsoundEditor.MapChanged -= scheduleUpdate;
        }

        /// <summary>
        /// Text which does something when clicked (e.g. seeking to a difference, or switching to a difficulty).
        /// </summary>
        private partial class ClickableText : OsuHoverContainer
        {
            public ClickableText(LocalisableString text, float size, FontWeight weight, Action action)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Action = action;

                Child = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: size, weight: weight))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = text,
                };
            }
        }
    }
}
