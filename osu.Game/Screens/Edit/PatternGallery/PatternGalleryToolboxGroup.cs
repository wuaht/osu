// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit.Hitsounding;
using osuTK;

namespace osu.Game.Screens.Edit.PatternGallery
{
    /// <summary>
    /// The pattern gallery in the right toolbox of the composer: a grid of the saved patterns, which can be inserted at the current time.
    /// </summary>
    public partial class PatternGalleryToolboxGroup : EditorToolboxGroup, IKeyBindingHandler<GlobalAction>
    {
        private const float tile_spacing = 6;
        private const float max_grid_height = 340;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorClock clock { get; set; } = null!;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private HitsoundDifficultyAutoApplier? hitsoundAutoApplier { get; set; }

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private PatternStore store = null!;

        private readonly Bindable<bool> scaleToTiming = new Bindable<bool>();
        private readonly Bindable<bool> snapToDivisor = new Bindable<bool>();
        private readonly Bindable<bool> preserveSliderVelocity = new Bindable<bool>();
        private readonly Bindable<bool> currentModeOnly = new Bindable<bool>();

        private readonly Bindable<Pattern?> selected = new Bindable<Pattern?>();

        // tiles are kept between refreshes, such that their previews are only drawn once.
        private readonly Dictionary<Pattern, PatternTile> tiles = new Dictionary<Pattern, PatternTile>();

        private SearchTextBox searchBox = null!;
        private OsuScrollContainer gridScroll = null!;
        private FillFlowContainer<PatternTile> grid = null!;
        private FillFlowContainer details = null!;
        private Container detailPreview = null!;
        private OsuTextBox nameBox = null!;
        private OsuTextFlowContainer statsText = null!;
        private LinkFlowContainer statusText = null!;
        private FillFlowContainer body = null!;
        private OsuSpriteText summary = null!;

        [Resolved]
        private IExpandingContainer? expandingContainer { get; set; }

        public PatternGalleryToolboxGroup()
            : base("pattern gallery")
        {
        }

        [BackgroundDependencyLoader]
        private void load(Storage storage, OsuConfigManager config, OsuColour colours)
        {
            store = new PatternStore(storage);

            config.BindWith(OsuSetting.SlopPatternGalleryScaleToTiming, scaleToTiming);
            config.BindWith(OsuSetting.SlopPatternGallerySnapToDivisor, snapToDivisor);
            config.BindWith(OsuSetting.SlopPatternGalleryPreserveSliderVelocity, preserveSliderVelocity);
            config.BindWith(OsuSetting.SlopPatternGalleryCurrentModeOnly, currentModeOnly);

            Children = new Drawable[]
            {
                // the toolbox is too narrow for the gallery while contracted.
                summary = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                    Colour = colourProvider.Content2,
                },
                body = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = SlopEditorStrings.PatternSaveSelection,
                    Action = () => SaveSelection(),
                },
                searchBox = new BasicSearchTextBox
                {
                    RelativeSizeAxes = Axes.X,
                    PlaceholderText = SlopEditorStrings.PatternSearch,
                    HoldFocus = false,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 4),
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 4),
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 4),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable?[]
                        {
                            new ToggleChip(SlopEditorStrings.PatternModeToggle, SlopEditorStrings.PatternCurrentModeOnly, currentModeOnly),
                            null,
                            new ToggleChip(SlopEditorStrings.PatternScaleToggle, SlopEditorStrings.PatternScaleToTiming, scaleToTiming),
                            null,
                            new ToggleChip(SlopEditorStrings.PatternSnapToggle, SlopEditorStrings.PatternSnapToDivisor, snapToDivisor),
                            null,
                            new ToggleChip(SlopEditorStrings.PatternSliderVelocityToggle, SlopEditorStrings.PatternPreserveSliderVelocity, preserveSliderVelocity),
                        }
                    }
                },
                gridScroll = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.X,
                    ScrollbarOverlapsContent = false,
                    Child = grid = new FillFlowContainer<PatternTile>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Full,
                        Spacing = new Vector2(tile_spacing),
                    },
                },
                details = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 150,
                            Masking = true,
                            CornerRadius = 6,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = colourProvider.Background5,
                                },
                                detailPreview = new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding(8),
                                },
                            }
                        },
                        nameBox = new OsuTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            PlaceholderText = SlopEditorStrings.PatternName,
                            CommitOnFocusLost = true,
                        },
                        statsText = new OsuTextFlowContainer(s => s.Font = OsuFont.GetFont(size: 12))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Colour = colourProvider.Content2,
                        },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            ColumnDimensions = new[]
                            {
                                new Dimension(),
                                new Dimension(GridSizeMode.Absolute, 4),
                                new Dimension(),
                                new Dimension(GridSizeMode.Absolute, 4),
                                new Dimension(),
                            },
                            Content = new[]
                            {
                                new Drawable?[]
                                {
                                    new RoundedButton
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Text = SlopEditorStrings.PatternInsert,
                                        Action = () => withSelected(Insert),
                                    },
                                    null,
                                    new RoundedButton
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Text = SlopEditorStrings.PatternOverwrite,
                                        Action = () => withSelected(p => SaveSelection(p)),
                                    },
                                    null,
                                    new RoundedButton
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Text = SlopEditorStrings.PatternDelete,
                                        BackgroundColour = colours.DangerousButtonColour,
                                        Action = () => withSelected(delete),
                                    },
                                }
                            }
                        },
                    }
                },
                statusText = new LinkFlowContainer(s => s.Font = OsuFont.GetFont(size: 12))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Colour = colourProvider.Content2,
                },
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            expandingContainer?.Expanded.BindValueChanged(e =>
            {
                body.FadeTo(e.NewValue ? 1 : 0, 150, Easing.OutQuint);
                summary.FadeTo(e.NewValue ? 0 : 1, 150, Easing.OutQuint);
            }, true);

            searchBox.Current.BindValueChanged(_ => refresh());
            currentModeOnly.BindValueChanged(_ => refresh());
            selected.BindValueChanged(_ => updateDetails(), true);
            nameBox.OnCommit += (_, _) => rename();
            refresh();
        }

        protected override void Update()
        {
            base.Update();

            float tileWidth = (grid.DrawWidth - tile_spacing) / 2;

            foreach (var tile in grid)
                tile.Width = tileWidth;

            gridScroll.Height = Math.Min(grid.DrawHeight, max_grid_height);
        }

        /// <summary>
        /// The shown patterns: the ones matching the search, most recently used first.
        /// </summary>
        private IEnumerable<Pattern> visiblePatterns => store.Patterns
                                                             .Where(p => !currentModeOnly.Value || p.RulesetId == editorBeatmap.BeatmapInfo.Ruleset.OnlineID)
                                                             .Where(p => p.Matches(searchBox.Current.Value ?? string.Empty))
                                                             .OrderByDescending(p => p.LastUsedUtc);

        private void refresh(Action<LinkFlowContainer>? status = null)
        {
            var patterns = visiblePatterns.ToList();

            grid.Clear(false);
            grid.AddRange(patterns.Select(tileOf));

            if (selected.Value != null && !store.Patterns.Contains(selected.Value))
                selected.Value = null;

            statusText.Clear();
            summary.Text = SlopEditorStrings.PatternCount(patterns.Count, store.Patterns.Count);

            if (status != null)
                status(statusText);
            else
                statusText.AddText(store.Patterns.Count == 0 ? SlopEditorStrings.PatternEmpty : SlopEditorStrings.PatternCount(patterns.Count, store.Patterns.Count));

            statusText.AddText(@" · ");
            statusText.AddLink(SlopEditorStrings.PatternImportMappingTools, importMappingTools);
        }

        private void refresh(LocalisableString status) => refresh(s => s.AddText(status));

        private PatternTile tileOf(Pattern pattern)
        {
            if (!tiles.TryGetValue(pattern, out var tile))
            {
                tiles[pattern] = tile = new PatternTile(store, pattern, selected)
                {
                    Insert = () => Insert(pattern),
                };
            }

            return tile;
        }

        /// <summary>
        /// Drops the cached tile of a pattern, such that its preview is drawn again.
        /// </summary>
        private void invalidateTile(Pattern pattern)
        {
            if (!tiles.Remove(pattern, out var tile))
                return;

            grid.Remove(tile, false);
            tile.Dispose();
        }

        private void updateDetails()
        {
            var pattern = selected.Value;

            detailPreview.Clear();

            if (pattern == null)
            {
                details.FadeOut(100, Easing.OutQuint);
                return;
            }

            details.FadeIn(100, Easing.OutQuint);
            detailPreview.Child = new DelayedLoadWrapper(() => new PatternPreview(store, pattern) { RelativeSizeAxes = Axes.Both }, 0)
            {
                RelativeSizeAxes = Axes.Both,
            };

            nameBox.Text = pattern.Name;
            statsText.Text = $@"{pattern.Breakdown}{Environment.NewLine}"
                             + SlopEditorStrings.PatternUsage($@"{pattern.DurationBeats:0.##}", $@"{pattern.SourceBpm:0}", $@"{pattern.SourceCircleSize:0.#}", pattern.UseCount);
        }

        private void withSelected(Action<Pattern> action)
        {
            if (selected.Value != null)
                action(selected.Value);
        }

        private void rename()
        {
            var pattern = selected.Value;

            if (pattern == null || pattern.Name == nameBox.Text)
                return;

            pattern.Name = nameBox.Text;
            store.SaveIndex();

            invalidateTile(pattern);
            refresh();
        }

        private void delete(Pattern pattern)
        {
            store.Delete(pattern);
            invalidateTile(pattern);
            selected.Value = null;

            refresh(s =>
            {
                s.AddText(SlopEditorStrings.PatternDeleted(pattern.DisplayName));
                s.AddText(@" ");
                s.AddLink(SlopEditorStrings.PatternUndo, () =>
                {
                    store.Restore(pattern);
                    refresh();
                    selected.Value = pattern;
                });
            });
        }

        /// <summary>
        /// Saves the selected hitobjects as a new pattern, or replaces the hitobjects of the given pattern by them.
        /// </summary>
        public void SaveSelection(Pattern? overwrite = null)
        {
            var selection = editorBeatmap.SelectedHitObjects.ToList();

            if (selection.Count == 0)
            {
                Expanded.Value = true;
                refresh(SlopEditorStrings.PatternNothingSelected);
                return;
            }

            if (editorBeatmap.BeatmapInfo.Ruleset.OnlineID is < 0 or > 3)
            {
                Expanded.Value = true;
                refresh(SlopEditorStrings.PatternUnsupportedRuleset);
                return;
            }

            Pattern pattern;

            if (overwrite != null)
            {
                store.Overwrite(overwrite, editorBeatmap, selection);
                invalidateTile(overwrite);
                pattern = overwrite;
            }
            else
                pattern = store.Add(string.Empty, editorBeatmap, selection);

            Expanded.Value = true;
            refresh();

            selected.Value = pattern;
            selected.TriggerChange();

            // like the gallery of stable: a new pattern is named right after saving it.
            if (overwrite == null)
                GetContainingFocusManager()?.ChangeFocus(nameBox);
        }

        /// <summary>
        /// Inserts the most recently used pattern which is shown.
        /// </summary>
        public void InsertMostRecent()
        {
            var pattern = visiblePatterns.FirstOrDefault();

            if (pattern != null)
                Insert(pattern);
        }

        /// <summary>
        /// Inserts a pattern at the current time, and selects its hitobjects.
        /// </summary>
        public void Insert(Pattern pattern)
        {
            List<HitObject> objects;

            try
            {
                var beatmap = store.Open(pattern) ?? throw new InvalidOperationException(@"The pattern file could not be read.");

                objects = PatternPlacement.Place(beatmap, editorBeatmap, editorBeatmap.BeatmapInfo.Ruleset, beatSnapProvider.SnapTime(clock.CurrentTime),
                    scaleToTiming.Value, snapToDivisor.Value ? t => beatSnapProvider.SnapTime(t) : null, preserveSliderVelocity.Value);
            }
            catch (Exception e)
            {
                Logger.Log($@"Failed to insert pattern ""{pattern.DisplayName}"": {e.Message}");
                refresh(SlopEditorStrings.PatternOpenFailed);
                return;
            }

            if (objects.Count == 0)
                return;

            editorBeatmap.BeginChange();
            editorBeatmap.SelectedHitObjects.Clear();
            hitsoundAutoApplier?.MarkPasted(objects);
            editorBeatmap.AddRange(objects);
            editorBeatmap.SelectedHitObjects.AddRange(objects);
            editorBeatmap.EndChange();

            store.MarkUsed(pattern);
            refresh();

            if (selected.Value == pattern)
                updateDetails();
        }

        private void importMappingTools() => refresh(SlopEditorStrings.PatternsImported(store.ImportMappingTools()));

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.EditorPatternGalleryToggle:
                    Expanded.Toggle();
                    return true;

                case GlobalAction.EditorPatternGallerySave:
                    SaveSelection();
                    return true;

                case GlobalAction.EditorPatternGalleryInsertRecent:
                    InsertMostRecent();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            // tiles which are not shown are not children, so they are not disposed with this group.
            foreach (var tile in tiles.Values.Where(t => t.Parent == null))
                tile.Dispose();
        }

        private partial class ToggleChip : OsuClickableContainer
        {
            private readonly LocalisableString label;
            private readonly Bindable<bool> current;

            private Box background = null!;
            private OsuSpriteText text = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public ToggleChip(LocalisableString label, LocalisableString tooltip, Bindable<bool> current)
            {
                this.label = label;
                this.current = current;

                TooltipText = tooltip;
                RelativeSizeAxes = Axes.X;
                Height = 24;
                Masking = true;
                CornerRadius = 5;
                Action = () => current.Value = !current.Value;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                AddRange(new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    text = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = label,
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                    },
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                current.BindValueChanged(on =>
                {
                    background.FadeColour(on.NewValue ? colourProvider.Colour3 : colourProvider.Background5, 150, Easing.OutQuint);
                    text.FadeColour(on.NewValue ? colourProvider.Background6 : colourProvider.Content2, 150, Easing.OutQuint);
                }, true);
            }
        }

        private partial class PatternTile : OsuClickableContainer
        {
            public Action Insert = null!;

            private readonly PatternStore store;
            private readonly Pattern pattern;
            private readonly Bindable<Pattern?> selected;

            private Container previewArea = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public PatternTile(PatternStore store, Pattern pattern, Bindable<Pattern?> selected)
            {
                this.store = store;
                this.pattern = pattern;
                this.selected = selected.GetBoundCopy();

                AutoSizeAxes = Axes.Y;
                TooltipText = SlopEditorStrings.PatternInsertTooltip;
                Action = () => this.selected.Value = pattern;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 2),
                    Children = new Drawable[]
                    {
                        previewArea = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 80,
                            Masking = true,
                            CornerRadius = 5,
                            BorderColour = colourProvider.Highlight1,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = colourProvider.Background5,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding(4),
                                    Child = new DelayedLoadWrapper(() => new PatternPreview(store, pattern) { RelativeSizeAxes = Axes.Both })
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                    },
                                },
                            }
                        },
                        new TruncatingSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = pattern.DisplayName,
                            Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold),
                        },
                        new TruncatingSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = $@"{pattern.ObjectCount} obj · {pattern.DurationBeats:0.#} beats",
                            Font = OsuFont.GetFont(size: 11),
                            Colour = colourProvider.Content2,
                        },
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                selected.BindValueChanged(_ => updateBorder(), true);
            }

            private void updateBorder() => previewArea.BorderThickness = selected.Value == pattern ? 3 : IsHovered ? 1.5f : 0;

            protected override bool OnHover(HoverEvent e)
            {
                updateBorder();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateBorder();
                base.OnHoverLost(e);
            }

            protected override bool OnDoubleClick(DoubleClickEvent e)
            {
                Insert();
                return true;
            }
        }
    }
}
