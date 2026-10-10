// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Rulesets.Edit;
using osuTK;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The tools of the hitsound editor: editing the targeted hitsounds, hitsound difficulties, and copying hitsounds between difficulties.
    /// Like the toolboxes of the compose screen, it contracts while the editor's sidebars are contracted.
    /// Unlike them, it doesn't expand on hover, as it shows compact tools for the targeted hitsounds while contracted.
    /// </summary>
    public partial class HitsoundToolbox : ExpandingToolboxContainer
    {
        public const float CONTRACTED_WIDTH = 150;
        public const float EXPANDED_WIDTH = 320;

        /// <summary>
        /// The width which the toolbox takes up, which the lanes leave free.
        /// </summary>
        public float ReservedWidth => DrawWidth;

        protected override bool ExpandOnHover => false;

        private readonly BindableBool contractSidebars = new BindableBool();

        private IconButton contractButton = null!;
        private Container contractedContent = null!;
        private Drawable expandedContent = null!;

        public HitsoundToolbox()
            : base(CONTRACTED_WIDTH, EXPANDED_WIDTH)
        {
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.EditorContractSidebars, contractSidebars);

            Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 20,
                    Child = contractButton = new IconButton
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Size = new Vector2(20),
                        IconScale = new Vector2(0.7f),
                        Margin = new MarginPadding { Right = 5 },
                        Action = () => contractSidebars.Toggle(),
                    },
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        contractedContent = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Child = new ToolboxGroup(SlopHitsoundEditorStrings.Hitsounds)
                            {
                                Child = new CompactTargetTools(),
                            },
                        },
                        // the expanded content keeps its width, such that it doesn't reflow while the toolbox expands or contracts.
                        expandedContent = new FillFlowContainer
                        {
                            Width = EXPANDED_WIDTH,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(5),
                            Children = new Drawable[]
                            {
                                new ToolboxGroup(SlopHitsoundEditorStrings.Hitsounds)
                                {
                                    Child = new TargetTools(),
                                },
                                new ToolboxGroup(SlopHitsoundEditorStrings.HitsoundDifficulty)
                                {
                                    Child = new HitsoundDifficultyTools(),
                                },
                                new ToolboxGroup(SlopHitsoundEditorStrings.CopyHitsounds)
                                {
                                    Child = new CopyTools(),
                                },
                            }
                        },
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Expanded.BindValueChanged(e =>
            {
                expandedContent.FadeTo(e.NewValue ? 1 : 0, 200, Easing.OutQuint);
                contractedContent.FadeTo(e.NewValue ? 0 : 1, 200, Easing.OutQuint);
            }, true);

            contractSidebars.BindValueChanged(c =>
            {
                contractButton.Icon = c.NewValue ? FontAwesome.Solid.AngleDoubleLeft : FontAwesome.Solid.AngleDoubleRight;
                contractButton.TooltipText = c.NewValue ? SlopHitsoundEditorStrings.ExpandSidebars : EditorStrings.ContractSidebars;
            }, true);
        }

        private partial class ToolboxGroup : SettingsToolboxGroup
        {
            public ToolboxGroup(LocalisableString title)
                : base(title)
            {
                RelativeSizeAxes = Axes.X;
                Width = 1;
                Spacing = new Vector2(0, 8);
            }
        }

        /// <summary>
        /// Whether all, some or none of the items have a property.
        /// </summary>
        private static HitsoundToggleState getState<T>(IReadOnlyCollection<T> items, Func<T, bool> predicate)
        {
            int count = items.Count(predicate);

            if (count == 0)
                return HitsoundToggleState.Off;

            return count == items.Count ? HitsoundToggleState.On : HitsoundToggleState.Mixed;
        }

        /// <summary>
        /// Creates a row of equally wide buttons, with an optional caption above.
        /// </summary>
        private static Drawable createRow(LocalisableString caption, params Drawable[] buttons)
        {
            var columns = new Drawable[buttons.Length * 2 - 1];

            for (int i = 0; i < buttons.Length; i++)
            {
                columns[i * 2] = buttons[i];

                if (i > 0)
                    columns[i * 2 - 1] = Empty();
            }

            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(3),
            };

            if (caption != default)
                flow.Add(createCaption(caption));

            flow.Add(new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                ColumnDimensions = columns.Select((_, i) => i % 2 == 0 ? new Dimension() : new Dimension(GridSizeMode.Absolute, 4)).ToArray(),
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                Content = new[] { columns },
            });

            return flow;
        }

        private static OsuSpriteText createCaption(LocalisableString caption) => new OsuSpriteText
        {
            Text = caption,
            Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
        };

        private static RoundedButton createButton(LocalisableString text, LocalisableString tooltip, Action action) => new TooltipButton
        {
            RelativeSizeAxes = Axes.X,
            Height = 30,
            Text = text,
            TooltipText = tooltip,
            Action = action,
        };

        /// <summary>
        /// Creates a button which toggles a setting.
        /// </summary>
        private static HitsoundToggleButton createToggle(LocalisableString label, BindableBool setting, LocalisableString tooltip = default)
        {
            var button = new HitsoundToggleButton(label, Colour4.White)
            {
                Outlined = true,
                TooltipText = tooltip,
                Action = setting.Toggle,
            };

            setting.BindValueChanged(v => button.State = v.NewValue ? HitsoundToggleState.On : HitsoundToggleState.Off, true);
            return button;
        }

        /// <summary>
        /// Displays the hitsounds which the tools act on: the selected hitsounds, or the hitsound at the playhead.
        /// </summary>
        private abstract partial class TargetDisplay : CompositeDrawable
        {
            [Resolved]
            protected HitsoundEditor HitsoundEditor { get; private set; } = null!;

            [Resolved]
            protected HitsoundScreen Screen { get; private set; } = null!;

            [Resolved]
            private EditorClock clock { get; set; } = null!;

            private bool stateInvalidated = true;
            private int? lastPlayheadColumn;

            protected TargetDisplay()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                HitsoundEditor.SelectionChanged += InvalidateState;
                HitsoundEditor.MapChanged += InvalidateState;
            }

            protected void InvalidateState() => stateInvalidated = true;

            protected override void Update()
            {
                base.Update();

                // without a selection, the hitsound at the playhead is targeted, which changes while the track plays or seeks.
                // while the track plays, the hitsounds at the playhead only last for a moment, so the display isn't updated to avoid flickering.
                if (HitsoundEditor.SelectedKeys.Count == 0 && !clock.IsRunning)
                {
                    int? playheadColumn = HitsoundEditor.Map.FindClosestColumn(clock.CurrentTime, HitsoundEditor.PASTE_LENIENCY)?.Key;

                    if (playheadColumn != lastPlayheadColumn)
                    {
                        lastPlayheadColumn = playheadColumn;
                        stateInvalidated = true;
                    }
                }

                if (stateInvalidated)
                {
                    stateInvalidated = false;
                    UpdateState(Screen.GetTargetColumns());
                }
            }

            protected abstract void UpdateState(IReadOnlyList<HitsoundColumn> columns);

            protected LocalisableString GetSummary(IReadOnlyList<HitsoundColumn> columns)
            {
                if (HitsoundEditor.SelectedKeys.Count > 0)
                    return SlopHitsoundEditorStrings.SelectedCount(columns.Count);

                return columns.Count > 0 ? SlopHitsoundEditorStrings.EditingAtPlayhead : SlopHitsoundEditorStrings.NothingSelected;
            }

            protected override void Dispose(bool isDisposing)
            {
                if (HitsoundEditor.IsNotNull())
                {
                    HitsoundEditor.SelectionChanged -= InvalidateState;
                    HitsoundEditor.MapChanged -= InvalidateState;
                }

                base.Dispose(isDisposing);
            }
        }

        /// <summary>
        /// Compact tools for the targeted hitsounds, shown while the toolbox is contracted.
        /// The buttons are labelled with the first letter of what they set, and show the full name in their tooltip.
        /// </summary>
        private partial class CompactTargetTools : TargetDisplay
        {
            private OsuSpriteText summary = null!;

            private readonly Dictionary<string, HitsoundToggleButton> normalBankButtons = new Dictionary<string, HitsoundToggleButton>();
            private readonly Dictionary<string, HitsoundToggleButton> additionButtons = new Dictionary<string, HitsoundToggleButton>();
            private readonly Dictionary<string, HitsoundToggleButton> additionBankButtons = new Dictionary<string, HitsoundToggleButton>();
            private HitsoundToggleButton autoAdditionBankButton = null!;

            private HitsoundToggleButton volumeDownButton = null!;
            private HitsoundToggleButton volumeUpButton = null!;
            private HitsoundToggleButton customIndexDownButton = null!;
            private HitsoundToggleButton customIndexUpButton = null!;
            private TruncatingSpriteText volumeText = null!;
            private TruncatingSpriteText customIndexText = null!;
            private RoundedButton clearButton = null!;

            [BackgroundDependencyLoader]
            private void load()
            {
                foreach (string bank in HitsoundLane.BANKS)
                {
                    string b = bank;
                    var colour = HitsoundLane.GetBankColour(bank);

                    normalBankButtons[bank] = new HitsoundToggleButton(initial(HitsoundLaneHeaders.GetBankName(bank)), colour)
                    {
                        TooltipText = HitsoundLaneHeaders.GetBankName(bank),
                        Action = () => HitsoundEditor.SetNormalBank(Screen.GetTargetColumns(), b),
                    };

                    additionBankButtons[bank] = new HitsoundToggleButton(initial(HitsoundLaneHeaders.GetBankName(bank)), colour)
                    {
                        TooltipText = HitsoundLaneHeaders.GetBankName(bank),
                        Action = () => HitsoundEditor.SetAdditionBank(Screen.GetTargetColumns(), b),
                    };
                }

                foreach (string addition in HitSampleInfo.ALL_ADDITIONS)
                {
                    string a = addition;

                    additionButtons[addition] = new HitsoundToggleButton(initial(HitsoundLaneHeaders.GetSampleName(addition)), Colour4.White)
                    {
                        Outlined = true,
                        TooltipText = HitsoundLaneHeaders.GetSampleName(addition),
                        Action = () => HitsoundEditor.ToggleAddition(Screen.GetTargetColumns(), a),
                    };
                }

                autoAdditionBankButton = new HitsoundToggleButton(initial(SlopHitsoundEditorStrings.Auto), Colour4.White)
                {
                    Outlined = true,
                    TooltipText = SlopHitsoundEditorStrings.Auto,
                    Action = () => HitsoundEditor.SetAdditionBank(Screen.GetTargetColumns(), null),
                };

                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(6),
                    Children = new Drawable[]
                    {
                        summary = new OsuSpriteText { Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold) },
                        createRow(SlopHitsoundEditorStrings.HitnormalBank, HitsoundLane.BANKS.Select(b => (Drawable)normalBankButtons[b]).ToArray()),
                        createRow(SlopHitsoundEditorStrings.Additions, HitSampleInfo.ALL_ADDITIONS.Select(a => (Drawable)additionButtons[a]).ToArray()),
                        createRow(SlopHitsoundEditorStrings.AdditionBank, HitsoundLane.BANKS.Select(b => (Drawable)additionBankButtons[b]).Prepend(autoAdditionBankButton).ToArray()),
                        createStepper(SlopHitsoundEditorStrings.Volume, out volumeDownButton, out volumeText, out volumeUpButton, changeVolume),
                        createStepper(SlopHitsoundEditorStrings.CustomIndex, out customIndexDownButton, out customIndexText, out customIndexUpButton, changeCustomIndex),
                        createToggle(SlopHitsoundEditorStrings.HitsoundDifficultyModeShort, HitsoundEditor.HitsoundDifficultyMode, SlopHitsoundEditorStrings.HitsoundDifficultyModeHint),
                        clearButton = createButton(SlopHitsoundEditorStrings.ClearAdditions, SlopHitsoundEditorStrings.ClearAdditionsTooltip,
                            () => Screen.DeleteOrClear(Screen.GetTargetColumns())),
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                HitsoundEditor.HitsoundDifficultyMode.BindValueChanged(m =>
                {
                    // in hitsound difficulty mode, hitsounds are deleted instead (see HitsoundScreen.DeleteOrClear).
                    clearButton.Text = m.NewValue ? SlopHitsoundEditorStrings.DeleteHitsounds : SlopHitsoundEditorStrings.ClearAdditions;
                    clearButton.TooltipText = m.NewValue ? SlopHitsoundEditorStrings.DeleteHitsoundsTooltip : SlopHitsoundEditorStrings.ClearAdditionsTooltip;
                }, true);
            }

            /// <summary>
            /// Creates a row which changes a value in steps, with buttons on both sides of the value.
            /// </summary>
            private static Drawable createStepper(LocalisableString caption, out HitsoundToggleButton down, out TruncatingSpriteText value, out HitsoundToggleButton up, Action<int> step)
            {
                down = new HitsoundToggleButton(@"-", Colour4.White) { Outlined = true, Action = () => step(-1) };
                up = new HitsoundToggleButton(@"+", Colour4.White) { Outlined = true, Action = () => step(1) };

                value = new TruncatingSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.X,
                    Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                };

                var flow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(3),
                    Children = new Drawable[]
                    {
                        createCaption(caption),
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            ColumnDimensions = new[]
                            {
                                new Dimension(GridSizeMode.Absolute, 28),
                                new Dimension(),
                                new Dimension(GridSizeMode.Absolute, 28),
                            },
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    down,
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 28,
                                        Padding = new MarginPadding { Horizontal = 4 },
                                        Child = value,
                                    },
                                    up,
                                }
                            },
                        },
                    }
                };

                return flow;
            }

            private void changeVolume(int direction)
            {
                var columns = Screen.GetTargetColumns();

                // volumes are changed in steps of 5, like when drawing them.
                HitsoundEditor.SetVolumes(columns.Select(c => (c, c.Volume + direction * 5)).ToList());
            }

            private void changeCustomIndex(int direction)
            {
                var columns = Screen.GetTargetColumns();

                if (columns.Count == 0)
                    return;

                HitsoundEditor.SetCustomIndex(columns, Math.Max(0, columns.Min(c => c.CustomIndex) + direction));
            }

            protected override void UpdateState(IReadOnlyList<HitsoundColumn> columns)
            {
                bool hasTargets = columns.Count > 0;

                summary.Text = GetSummary(columns);

                foreach (var (bank, button) in normalBankButtons)
                {
                    button.Enabled.Value = hasTargets;
                    button.State = getState(columns, c => c.Has(new HitsoundLane(bank, HitSampleInfo.HIT_NORMAL)));
                }

                foreach (var (addition, button) in additionButtons)
                {
                    button.Enabled.Value = hasTargets;
                    button.State = getState(columns, c => c.Targets.Any(t => t.Samples.Any(s => s.Name == addition)));
                }

                // the addition bank only applies to hitsounds with additions.
                var additions = columns.Select(c => c.Targets.SelectMany(t => t.Samples).Where(HitsoundSamples.IsAddition).ToList()).Where(a => a.Count > 0).ToList();

                foreach (var (bank, button) in additionBankButtons)
                {
                    button.Enabled.Value = additions.Count > 0;
                    button.State = getState(additions, a => a.Any(s => s.Bank == bank && !s.EditorAutoBank));
                }

                autoAdditionBankButton.Enabled.Value = additions.Count > 0;
                autoAdditionBankButton.State = getState(additions, a => a.All(s => s.EditorAutoBank));

                foreach (var button in new[] { volumeDownButton, volumeUpButton, customIndexDownButton, customIndexUpButton })
                    button.Enabled.Value = hasTargets;

                clearButton.Enabled.Value = hasTargets;

                if (!hasTargets)
                {
                    volumeText.Text = @"-";
                    customIndexText.Text = @"-";
                    return;
                }

                int minVolume = columns.Min(c => c.Volume);
                int maxVolume = columns.Max(c => c.Volume);
                volumeText.Text = minVolume == maxVolume ? $@"{maxVolume}%" : $@"{minVolume}-{maxVolume}%";

                var indices = columns.Select(c => c.CustomIndex).Distinct().Order().ToList();
                customIndexText.Text = indices.Count == 1 ? HitsoundLaneHeaders.GetCustomIndexName(indices[0]) : string.Join(@", ", indices);
            }

            private static LocalisableString initial(LocalisableString name)
            {
                string text = name.ToString();
                return text.Length > 0 ? text[..1] : text;
            }
        }

        /// <summary>
        /// Edits the targeted hitsounds.
        /// </summary>
        private partial class TargetTools : TargetDisplay
        {
            [Resolved]
            private EditorBeatmap editorBeatmap { get; set; } = null!;

            private OsuSpriteText summary = null!;

            private readonly Dictionary<string, HitsoundToggleButton> normalBankButtons = new Dictionary<string, HitsoundToggleButton>();
            private readonly Dictionary<string, HitsoundToggleButton> additionButtons = new Dictionary<string, HitsoundToggleButton>();
            private readonly Dictionary<string, HitsoundToggleButton> additionBankButtons = new Dictionary<string, HitsoundToggleButton>();
            private HitsoundToggleButton autoAdditionBankButton = null!;

            private readonly BindableInt volume = new BindableInt(100)
            {
                MinValue = 5,
                MaxValue = 100,
            };

            private readonly Bindable<CustomIndexItem> customIndex = new Bindable<CustomIndexItem>(new CustomIndexItem(0));

            private FormDropdown<CustomIndexItem> customIndexDropdown = null!;
            private FormSliderBar<int> volumeSlider = null!;
            private RoundedButton rampButton = null!;
            private RoundedButton clearButton = null!;

            private bool updatingState;

            [BackgroundDependencyLoader]
            private void load()
            {
                foreach (string bank in HitsoundLane.BANKS)
                {
                    string b = bank;
                    var colour = HitsoundLane.GetBankColour(bank);

                    normalBankButtons[bank] = new HitsoundToggleButton(HitsoundLaneHeaders.GetBankName(bank), colour)
                    {
                        Action = () => HitsoundEditor.SetNormalBank(Screen.GetTargetColumns(), b),
                    };

                    additionBankButtons[bank] = new HitsoundToggleButton(HitsoundLaneHeaders.GetBankName(bank), colour)
                    {
                        Action = () => HitsoundEditor.SetAdditionBank(Screen.GetTargetColumns(), b),
                    };
                }

                foreach (string addition in HitSampleInfo.ALL_ADDITIONS)
                {
                    string a = addition;

                    additionButtons[addition] = new HitsoundToggleButton(HitsoundLaneHeaders.GetSampleName(addition), Colour4.White)
                    {
                        Outlined = true,
                        Action = () => HitsoundEditor.ToggleAddition(Screen.GetTargetColumns(), a),
                    };
                }

                autoAdditionBankButton = new HitsoundToggleButton(SlopHitsoundEditorStrings.Auto, Colour4.White)
                {
                    Outlined = true,
                    Action = () => HitsoundEditor.SetAdditionBank(Screen.GetTargetColumns(), null),
                };

                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(8),
                    Children = new Drawable[]
                    {
                        summary = new OsuSpriteText { Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold) },
                        createRow(SlopHitsoundEditorStrings.HitnormalBank, HitsoundLane.BANKS.Select(b => (Drawable)normalBankButtons[b]).ToArray()),
                        createRow(SlopHitsoundEditorStrings.Additions, HitSampleInfo.ALL_ADDITIONS.Select(a => (Drawable)additionButtons[a]).ToArray()),
                        createRow(SlopHitsoundEditorStrings.AdditionBank, HitsoundLane.BANKS.Select(b => (Drawable)additionBankButtons[b]).Prepend(autoAdditionBankButton).ToArray()),
                        volumeSlider = new FormSliderBar<int>
                        {
                            Caption = SlopHitsoundEditorStrings.Volume,
                            Current = volume,
                            TransferValueOnCommit = true,
                            LabelFormat = v => $@"{v}%",
                            TooltipFormat = v => $@"{v}%",
                            PlaySamplesOnAdjust = false,
                        },
                        customIndexDropdown = new FormDropdown<CustomIndexItem>
                        {
                            Caption = SlopHitsoundEditorStrings.CustomIndex,
                            HintText = SlopHitsoundEditorStrings.CustomIndexHint,
                            Current = customIndex,
                        },
                        createRow(default,
                            rampButton = createButton(SlopHitsoundEditorStrings.RampVolume, SlopHitsoundEditorStrings.RampVolumeTooltip,
                                () => HitsoundEditor.RampVolume(HitsoundEditor.SelectedColumns)),
                            clearButton = createButton(SlopHitsoundEditorStrings.ClearAdditions, SlopHitsoundEditorStrings.ClearAdditionsTooltip,
                                () => Screen.DeleteOrClear(Screen.GetTargetColumns()))),
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                volume.BindValueChanged(v =>
                {
                    if (!updatingState)
                        HitsoundEditor.SetVolume(Screen.GetTargetColumns(), v.NewValue);
                });

                customIndex.BindValueChanged(i =>
                {
                    if (!updatingState)
                        HitsoundEditor.SetCustomIndex(Screen.GetTargetColumns(), i.NewValue.Index);
                });

                HitsoundEditor.HitsoundDifficultyMode.BindValueChanged(m =>
                {
                    // in hitsound difficulty mode, hitsounds are deleted instead (see HitsoundScreen.DeleteOrClear).
                    clearButton.Text = m.NewValue ? SlopHitsoundEditorStrings.DeleteHitsounds : SlopHitsoundEditorStrings.ClearAdditions;
                    clearButton.TooltipText = m.NewValue ? SlopHitsoundEditorStrings.DeleteHitsoundsTooltip : SlopHitsoundEditorStrings.ClearAdditionsTooltip;
                }, true);
            }

            protected override void UpdateState(IReadOnlyList<HitsoundColumn> columns)
            {
                bool hasTargets = columns.Count > 0;

                summary.Text = GetSummary(columns);

                foreach (var (bank, button) in normalBankButtons)
                {
                    button.Enabled.Value = hasTargets;
                    button.State = getState(columns, c => c.Has(new HitsoundLane(bank, HitSampleInfo.HIT_NORMAL)));
                }

                foreach (var (addition, button) in additionButtons)
                {
                    button.Enabled.Value = hasTargets;
                    button.State = getState(columns, c => c.Targets.Any(t => t.Samples.Any(s => s.Name == addition)));
                }

                // the addition bank only applies to hitsounds with additions.
                var withAdditions = columns.Where(c => c.Targets.Any(t => t.Samples.Any(HitsoundSamples.IsAddition))).ToList();
                var additions = withAdditions.Select(c => c.Targets.SelectMany(t => t.Samples).Where(HitsoundSamples.IsAddition).ToList()).ToList();

                foreach (var (bank, button) in additionBankButtons)
                {
                    button.Enabled.Value = withAdditions.Count > 0;
                    button.State = getState(additions, a => a.Any(s => s.Bank == bank && !s.EditorAutoBank));
                }

                autoAdditionBankButton.Enabled.Value = withAdditions.Count > 0;
                autoAdditionBankButton.State = getState(additions, a => a.All(s => s.EditorAutoBank));

                updatingState = true;

                try
                {
                    volumeSlider.Current.Disabled = false;
                    volume.Value = hasTargets ? columns.Max(c => c.Volume) : 100;
                    volumeSlider.Current.Disabled = !hasTargets;

                    updateCustomIndexItems(columns);
                }
                finally
                {
                    updatingState = false;
                }

                rampButton.Enabled.Value = HitsoundEditor.SelectedKeys.Count >= 3;
                clearButton.Enabled.Value = hasTargets;
            }

            private void updateCustomIndexItems(IReadOnlyList<HitsoundColumn> columns)
            {
                var indices = new SortedSet<int> { 0, 1 };

                if (editorBeatmap.BeatmapSkin != null)
                {
                    foreach (var set in editorBeatmap.BeatmapSkin.GetAvailableSampleSets())
                        indices.Add(set.SampleSetIndex);
                }

                foreach (var column in columns)
                    indices.Add(column.CustomIndex);

                // allow choosing the next unused index, for samples which are added later.
                indices.Add(indices.Max + 1);

                var items = indices.Select(i => new CustomIndexItem(i)).ToList();

                // replacing the items may change the current value, which isn't allowed while it is disabled.
                customIndex.Disabled = false;

                if (!customIndexDropdown.Items.SequenceEqual(items))
                    customIndexDropdown.Items = items;

                customIndex.Value = new CustomIndexItem(columns.Count > 0 ? columns[0].CustomIndex : 0);
                customIndex.Disabled = columns.Count == 0;
            }
        }

        private partial class HitsoundDifficultyTools : CompositeDrawable
        {
            [Resolved]
            private HitsoundEditor hitsoundEditor { get; set; } = null!;

            [Resolved]
            private HitsoundScreen screen { get; set; } = null!;

            private readonly Bindable<string> startingBank = new Bindable<string>(HitSampleInfo.BANK_NORMAL);

            private readonly BindableInt startingVolume = new BindableInt(100)
            {
                MinValue = 5,
                MaxValue = 100,
            };

            private readonly Dictionary<string, HitsoundToggleButton> bankButtons = new Dictionary<string, HitsoundToggleButton>();

            private readonly BindableBool removeObjects = new BindableBool(true);

            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                foreach (string bank in HitsoundLane.BANKS)
                {
                    string b = bank;

                    bankButtons[bank] = new HitsoundToggleButton(HitsoundLaneHeaders.GetBankName(bank), HitsoundLane.GetBankColour(bank))
                    {
                        Action = () => startingBank.Value = b,
                    };
                }

                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(8),
                    Children = new Drawable[]
                    {
                        new FormCheckBox
                        {
                            Caption = SlopHitsoundEditorStrings.HitsoundDifficultyMode,
                            HintText = SlopHitsoundEditorStrings.HitsoundDifficultyModeHint,
                            Current = hitsoundEditor.HitsoundDifficultyMode,
                        },
                        createRow(SlopHitsoundEditorStrings.StartingBank, HitsoundLane.BANKS.Select(b => (Drawable)bankButtons[b]).ToArray()),
                        new FormSliderBar<int>
                        {
                            Caption = SlopHitsoundEditorStrings.StartingVolume,
                            HintText = SlopHitsoundEditorStrings.StartingHitsoundsHint,
                            Current = startingVolume,
                            KeyboardStep = 5,
                            LabelFormat = v => $@"{v}%",
                            TooltipFormat = v => $@"{v}%",
                            PlaySamplesOnAdjust = false,
                        },
                        new FormCheckBox
                        {
                            Caption = SlopHitsoundEditorStrings.RemoveAllObjects,
                            HintText = SlopHitsoundEditorStrings.RemoveAllObjectsHint,
                            Current = removeObjects,
                        },
                        createButton(SlopHitsoundEditorStrings.CreateHitsoundDifficulty, SlopHitsoundEditorStrings.CreateHitsoundDifficultyTooltip,
                            () => screen.CreateHitsoundDifficulty(startingBank.Value, startingVolume.Value, removeObjects.Value)),
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // start with the values which the beatmap already uses, if it has hitsounds.
                var (bank, volume) = hitsoundEditor.GetBaseHitsounds();

                startingBank.Value = bank;
                startingVolume.Value = volume;

                startingBank.BindValueChanged(b =>
                {
                    foreach (var (buttonBank, button) in bankButtons)
                        button.State = buttonBank == b.NewValue ? HitsoundToggleState.On : HitsoundToggleState.Off;
                }, true);
            }
        }

        private partial class CopyTools : CompositeDrawable
        {
            [Resolved]
            private HitsoundScreen screen { get; set; } = null!;

            private readonly List<(BeatmapInfo difficulty, BindableBool selected)> difficulties = new List<(BeatmapInfo, BindableBool)>();

            private readonly BindableBool copySampleSets = new BindableBool(true);
            private readonly BindableBool copyAdditions = new BindableBool(true);
            private readonly BindableBool copyVolumes = new BindableBool(true);
            private readonly BindableBool preserveMutedVolumes = new BindableBool(true);
            private readonly BindableBool copyCustomIndices = new BindableBool(true);
            private readonly BindableBool copySliderBodies = new BindableBool(true);
            private readonly BindableBool overwriteUnmatched = new BindableBool(true);
            private readonly BindableBool muteUnmatchedSliderEnds = new BindableBool();

            private readonly BindableInt leniency = new BindableInt((int)new HitsoundCopyOptions().Leniency)
            {
                MinValue = 0,
                MaxValue = 20,
            };

            private readonly Bindable<DifficultyItem> importSource = new Bindable<DifficultyItem>();

            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                FillFlowContainer flow;

                InternalChild = flow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(8),
                };

                var others = screen.GetOtherDifficulties();

                if (others.Count == 0)
                {
                    flow.Add(new OsuSpriteText
                    {
                        Text = SlopHitsoundEditorStrings.NoOtherDifficulties,
                        Font = OsuFont.Default.With(size: 13),
                    });
                    return;
                }

                var difficultyFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(3),
                    Child = createCaption(SlopHitsoundEditorStrings.Difficulties),
                };

                foreach (var difficulty in others)
                {
                    // hitsound difficulties are usually the source of the hitsounds, not a target.
                    var selected = new BindableBool(!HitsoundEditor.IsHitsoundDifficultyName(difficulty.DifficultyName));

                    difficulties.Add((difficulty, selected));
                    difficultyFlow.Add(createToggle(difficulty.DifficultyName, selected, difficulty.Ruleset.Name));
                }

                var items = others.Select(d => new DifficultyItem(d)).ToList();
                importSource.Value = items.FirstOrDefault(i => HitsoundEditor.IsHitsoundDifficultyName(i.Difficulty.DifficultyName)) ?? items[0];

                flow.AddRange(new Drawable[]
                {
                    difficultyFlow,
                    createRow(SlopHitsoundEditorStrings.CopyWhat,
                        createToggle(SlopHitsoundEditorStrings.CopySampleSets, copySampleSets, SlopHitsoundEditorStrings.CopySampleSetsTooltip),
                        createToggle(SlopHitsoundEditorStrings.CopyAdditions, copyAdditions),
                        createToggle(SlopHitsoundEditorStrings.CopyVolumes, copyVolumes)),
                    createRow(default,
                        createToggle(SlopHitsoundEditorStrings.CopyCustomIndices, copyCustomIndices, SlopHitsoundEditorStrings.CopyCustomIndicesTooltip),
                        createToggle(SlopHitsoundEditorStrings.CopySliderBodies, copySliderBodies, SlopHitsoundEditorStrings.CopySliderBodiesTooltip)),
                    createRow(SlopHitsoundEditorStrings.CopyOptions,
                        createToggle(SlopHitsoundEditorStrings.PreserveMutedVolumes, preserveMutedVolumes, SlopHitsoundEditorStrings.PreserveMutedVolumesHint),
                        createToggle(SlopHitsoundEditorStrings.OverwriteUnmatched, overwriteUnmatched, SlopHitsoundEditorStrings.OverwriteUnmatchedHint)),
                    createRow(default,
                        createToggle(SlopHitsoundEditorStrings.MuteUnmatchedSliderEnds, muteUnmatchedSliderEnds, SlopHitsoundEditorStrings.MuteUnmatchedSliderEndsHint)),
                    new FormSliderBar<int>
                    {
                        Caption = SlopHitsoundEditorStrings.Leniency,
                        HintText = SlopHitsoundEditorStrings.LeniencyHint,
                        Current = leniency,
                        LabelFormat = v => $@"{v} ms",
                        TooltipFormat = v => $@"{v} ms",
                    },
                    createButton(SlopHitsoundEditorStrings.CopyToDifficulties, SlopHitsoundEditorStrings.CopyToDifficultiesTooltip,
                        () => screen.CopyToDifficulties(difficulties.Where(d => d.selected.Value).Select(d => d.difficulty).ToList(), createOptions())),
                    new FormDropdown<DifficultyItem>
                    {
                        Caption = SlopHitsoundEditorStrings.ImportSource,
                        Items = items,
                        Current = importSource,
                    },
                    createButton(SlopHitsoundEditorStrings.Import, SlopHitsoundEditorStrings.ImportTooltip,
                        () => screen.ImportFromDifficulty(importSource.Value.Difficulty, createOptions())),
                });
            }

            private HitsoundCopyOptions createOptions() => new HitsoundCopyOptions
            {
                Leniency = leniency.Value,
                CopySampleSets = copySampleSets.Value,
                CopyAdditions = copyAdditions.Value,
                CopyVolumes = copyVolumes.Value,
                PreserveMutedVolumes = preserveMutedVolumes.Value,
                CopyCustomIndices = copyCustomIndices.Value,
                CopySliderBodies = copySliderBodies.Value,
                OverwriteUnmatched = overwriteUnmatched.Value,
                MuteUnmatchedSliderEnds = muteUnmatchedSliderEnds.Value,
            };
        }

        /// <summary>
        /// A button whose tooltip highlights keywords like the other tooltips of the hitsound editor.
        /// </summary>
        private partial class TooltipButton : RoundedButton, IHasHitsoundTooltip
        {
        }

        private record CustomIndexItem(int Index)
        {
            public override string ToString()
            {
                switch (Index)
                {
                    case 0:
                        return SlopHitsoundEditorStrings.CustomIndexSkin.ToString();

                    case 1:
                        return SlopHitsoundEditorStrings.CustomIndexBeatmap.ToString();

                    default:
                        return SlopHitsoundEditorStrings.CustomIndexCustom(Index).ToString();
                }
            }
        }

        private record DifficultyItem(BeatmapInfo Difficulty)
        {
            public override string ToString() => Difficulty.DifficultyName;
        }
    }
}
