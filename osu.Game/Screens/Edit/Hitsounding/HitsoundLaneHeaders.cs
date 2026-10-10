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
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The names of the visible lanes, which play their sample when clicked, and can be muted.
    /// </summary>
    public partial class HitsoundLaneHeaders : CompositeDrawable
    {
        /// <summary>
        /// The opacity of the backgrounds of headers while the lanes are frosted, such that the frosted background shows through them.
        /// </summary>
        public const float FROSTED_HEADER_ALPHA = 0.45f;

        [Resolved]
        private HitsoundEditor hitsoundEditor { get; set; } = null!;

        public HitsoundLaneHeaders()
        {
            RelativeSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            hitsoundEditor.VisibleLanesChanged += updateLanes;
            updateLanes();
        }

        private void updateLanes()
        {
            var lanes = hitsoundEditor.VisibleLanes;

            ClearInternal();

            for (int i = 0; i < lanes.Count; i++)
            {
                AddInternal(new LaneHeader(lanes[i], i == 0 || lanes[i - 1].Bank != lanes[i].Bank)
                {
                    RelativePositionAxes = Axes.Y,
                    Y = (float)i / lanes.Count,
                    Height = 1f / lanes.Count,
                });
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (hitsoundEditor.IsNotNull())
                hitsoundEditor.VisibleLanesChanged -= updateLanes;

            base.Dispose(isDisposing);
        }

        public static LocalisableString GetBankName(string bank)
        {
            switch (bank)
            {
                case HitSampleInfo.BANK_SOFT:
                    return SlopHitsoundEditorStrings.BankSoft;

                case HitSampleInfo.BANK_DRUM:
                    return SlopHitsoundEditorStrings.BankDrum;

                default:
                    return SlopHitsoundEditorStrings.BankNormal;
            }
        }

        /// <summary>
        /// The name of a custom sample set, like "Custom 2".
        /// </summary>
        public static LocalisableString GetCustomIndexName(int customIndex)
        {
            switch (customIndex)
            {
                case 0:
                    return SlopHitsoundEditorStrings.CustomIndexSkin;

                case 1:
                    return SlopHitsoundEditorStrings.CustomIndexBeatmap;

                default:
                    return SlopHitsoundEditorStrings.CustomIndexCustom(customIndex);
            }
        }

        public static LocalisableString GetSampleName(string sample)
        {
            switch (sample)
            {
                case HitSampleInfo.HIT_WHISTLE:
                    return SlopHitsoundEditorStrings.SampleWhistle;

                case HitSampleInfo.HIT_FINISH:
                    return SlopHitsoundEditorStrings.SampleFinish;

                case HitSampleInfo.HIT_CLAP:
                    return SlopHitsoundEditorStrings.SampleClap;

                default:
                    return SlopHitsoundEditorStrings.SampleHitnormal;
            }
        }

        private partial class LaneHeader : CompositeDrawable, IHasContextMenu
        {
            private readonly HitsoundLane lane;
            private readonly bool firstOfBank;

            private IconButton muteButton = null!;
            private IconButton? expandButton;

            private NameButton previewButton = null!;
            private OsuSpriteText? subLaneName;
            private Container nameContainer = null!;
            private OsuTextBox? renameTextBox;

            [Resolved]
            private HitsoundLaneNames laneNames { get; set; } = null!;

            private readonly BindableList<HitsoundLane> mutedLanes = new BindableList<HitsoundLane>();

            private readonly BindableBool frosted = new BindableBool();

            private Box background = null!;

            [Resolved]
            private HitsoundEditor hitsoundEditor { get; set; } = null!;

            [Resolved]
            private HitsoundPlayback playback { get; set; } = null!;

            [Resolved]
            private EditorClock clock { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public LaneHeader(HitsoundLane lane, bool firstOfBank)
            {
                this.lane = lane;
                this.firstOfBank = firstOfBank;

                RelativeSizeAxes = Axes.Both;
            }

            /// <summary>
            /// The space on the left of the name, which fits the button expanding the sub-lanes.
            /// </summary>
            private const float name_padding = 24;

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config)
            {
                var colour = HitsoundLane.GetBankColour(lane.Bank);

                config.BindWith(OsuSetting.SlopHitsoundEditorFrostedLanes, frosted);

                Drawable name;

                if (lane.IsSubLane)
                {
                    // sub-lanes are indented below their lane, and only show the custom sample set, or the name given to them.
                    name = subLaneName = new TruncatingSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                        Colour = colour,
                        Alpha = 0.85f,
                    };
                }
                else
                {
                    name = new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(4, 0),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = GetBankName(lane.Bank),
                                Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                                Colour = colour,
                            },
                            new OsuSpriteText
                            {
                                Text = GetSampleName(lane.Sample),
                                Font = OsuFont.Default.With(size: 13),
                            },
                        }
                    };
                }

                InternalChildren = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = lane.IsSubLane ? colourProvider.Background5 : colourProvider.Background4,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = lane.IsSubLane ? 2 : 4,
                        Colour = colour,
                        Alpha = lane.IsSubLane ? 0.6f : 1,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = firstOfBank ? 2 : 1,
                        Colour = firstOfBank ? colourProvider.Background1 : colourProvider.Background3,
                    },
                    nameContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = lane.IsSubLane ? name_padding + 10 : name_padding, Right = 30 },
                        Child = previewButton = new NameButton
                        {
                            RelativeSizeAxes = Axes.Both,
                            TooltipText = getTooltip(),
                            Action = preview,
                            DoubleClickAction = lane.IsSubLane ? beginRename : toggleExpandedIfPossible,
                            Child = name,
                        },
                    },
                    muteButton = new IconButton
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Size = new Vector2(24),
                        IconScale = new Vector2(0.7f),
                        Action = toggleMute,
                    },
                };

                if (!lane.IsSubLane)
                {
                    AddInternal(expandButton = new IconButton
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 5,
                        Size = new Vector2(18),
                        IconScale = new Vector2(0.6f),
                        Action = () => hitsoundEditor.ToggleExpanded(lane),
                    });
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                mutedLanes.BindTo(hitsoundEditor.MutedLanes);
                mutedLanes.BindCollectionChanged((_, _) => updateMuteState(), true);

                frosted.BindValueChanged(f => background.Alpha = f.NewValue ? FROSTED_HEADER_ALPHA : 1, true);

                if (expandButton != null)
                {
                    hitsoundEditor.MapChanged += updateExpandButton;
                    updateExpandButton();
                }

                if (lane.IsSubLane)
                {
                    laneNames.NamesChanged += updateName;
                    updateName();
                }
            }

            private void updateName()
            {
                if (subLaneName == null || lane.CustomIndex is not int customIndex)
                    return;

                string? givenName = laneNames.Get(lane);

                subLaneName.Text = givenName ?? GetCustomIndexName(customIndex);
                previewButton.TooltipText = getTooltip();
            }

            public MenuItem[]? ContextMenuItems
            {
                get
                {
                    if (!lane.IsSubLane)
                        return null;

                    var items = new List<MenuItem> { new OsuMenuItem(SlopHitsoundEditorStrings.RenameLane, MenuItemType.Standard, beginRename) };

                    if (laneNames.Get(lane) != null)
                        items.Add(new OsuMenuItem(SlopHitsoundEditorStrings.ResetLaneName, MenuItemType.Destructive, () => laneNames.Set(lane, null)));

                    return items.ToArray();
                }
            }

            /// <summary>
            /// The name of the lane, which previews the sample when clicked.
            /// Double click events are only received by the drawable which handled the click, so they are handled here as well.
            /// </summary>
            private partial class NameButton : OsuClickableContainer, IHasHitsoundTooltip
            {

                // clicking plays the sample of the lane, which the click sound of the interface would play over.
                protected override HoverSounds CreateHoverSounds(HoverSampleSet sampleSet) => new HoverSounds(sampleSet) { Enabled = { BindTarget = Enabled } };

                public Action? DoubleClickAction { get; init; }

                protected override bool OnDoubleClick(DoubleClickEvent e)
                {
                    if (DoubleClickAction == null)
                        return false;

                    DoubleClickAction();
                    return true;
                }
            }

            /// <summary>
            /// Replaces the name with a text box, in which the sub-lane is renamed. Committing an empty name resets it.
            /// </summary>
            private void beginRename()
            {
                if (renameTextBox != null || subLaneName == null)
                    return;

                previewButton.Alpha = 0;

                nameContainer.Add(renameTextBox = new OsuTextBox
                {
                    RelativeSizeAxes = Axes.Both,
                    Height = 1,
                    Text = laneNames.Get(lane) ?? string.Empty,
                    PlaceholderText = subLaneName.Text,
                    CommitOnFocusLost = true,
                    SelectAllOnFocus = true,
                });

                renameTextBox.OnCommit += (textBox, _) =>
                {
                    laneNames.Set(lane, textBox.Text);
                    endRename();
                };

                // the text box can only be focused once it is alive, which is after the children have been updated.
                ScheduleAfterChildren(() => GetContainingFocusManager()?.ChangeFocus(renameTextBox));
            }

            private void endRename()
            {
                if (renameTextBox == null)
                    return;

                // the text box is removed after the commit finished, as it is still handling the commit.
                var textBox = renameTextBox;
                renameTextBox = null;
                Schedule(() => textBox.Expire());

                previewButton.Alpha = 1;
            }

            /// <summary>
            /// Shows the button expanding the sub-lanes only for lanes which have sub-lanes (as the beatmap has custom samples for them).
            /// </summary>
            /// <summary>
            /// Whether the lane can be expanded (as the beatmap has custom samples for it), or is expanded.
            /// </summary>
            private bool canExpand => !lane.IsSubLane && (hitsoundEditor.ExpandedLanes.Contains(lane) || hitsoundEditor.GetSubLaneIndices(lane).Count > 0);

            private void toggleExpandedIfPossible()
            {
                if (canExpand)
                    hitsoundEditor.ToggleExpanded(lane);
            }

            private void updateExpandButton()
            {
                if (expandButton == null)
                    return;

                bool expanded = hitsoundEditor.ExpandedLanes.Contains(lane);
                bool hasSubLanes = canExpand;

                previewButton.TooltipText = getTooltip();

                expandButton.Alpha = hasSubLanes ? 1 : 0;
                expandButton.Enabled.Value = hasSubLanes;
                expandButton.Icon = expanded ? FontAwesome.Solid.ChevronDown : FontAwesome.Solid.ChevronRight;
                expandButton.TooltipText = expanded ? SlopHitsoundEditorStrings.CollapseCustomSampleSets : SlopHitsoundEditorStrings.ExpandCustomSampleSets;
            }

            protected override void Update()
            {
                base.Update();

                // hide the buttons when lanes are too small to fit them.
                muteButton.Alpha = DrawHeight >= 14 ? 1 : 0;
                muteButton.Size = new Vector2(Math.Min(24, DrawHeight));

                if (expandButton != null)
                    expandButton.Size = new Vector2(Math.Min(18, DrawHeight));
            }

            protected override void Dispose(bool isDisposing)
            {
                if (hitsoundEditor.IsNotNull())
                    hitsoundEditor.MapChanged -= updateExpandButton;

                if (laneNames.IsNotNull())
                    laneNames.NamesChanged -= updateName;

                base.Dispose(isDisposing);
            }

            /// <summary>
            /// How to preview the lane, and for which sounds of a song its sample is commonly used.
            /// For sub-lanes, the full name of the sample and how to rename it are shown as well.
            /// </summary>
            private LocalisableString getTooltip()
            {
                var lines = new List<string>();

                if (lane.CustomIndex is int customIndex)
                {
                    lines.Add($@"{HitsoundSoundGuide.GetLaneName(lane.Parent)} - {GetCustomIndexName(customIndex)}");
                    lines.Add(SlopHitsoundEditorStrings.RenameLaneHint.ToString());
                }
                else if (canExpand)
                    lines.Add(SlopHitsoundEditorStrings.ExpandLaneHint.ToString());

                lines.Add(SlopHitsoundEditorStrings.PlaySample.ToString());

                var sounds = HitsoundSoundGuide.GetSoundsFor(lane.Parent).ToList();

                if (sounds.Count > 0)
                    lines.Add(SlopHitsoundEditorStrings.CommonlyUsedFor(string.Join(@", ", sounds)).ToString());

                return string.Join(Environment.NewLine, lines);
            }

            /// <summary>
            /// Plays the sample of the lane from the custom sample set of the hitsounds being edited, such that the beatmap's own samples can be heard.
            /// While control is held, the sample of the skin is played instead.
            /// </summary>
            private void preview()
            {
                bool useSkin = GetContainingInputManager()?.CurrentState.Keyboard.ControlPressed == true;

                if (useSkin)
                    playback.PlayLane(lane.Parent);
                else
                    playback.PlayLane(lane, customIndex: hitsoundEditor.GetPreviewCustomIndex(clock.CurrentTime));
            }

            private void toggleMute()
            {
                if (!mutedLanes.Remove(lane))
                    mutedLanes.Add(lane);
            }

            private void updateMuteState()
            {
                // muting a lane mutes its sub-lanes as well.
                bool mutedByParent = lane.IsSubLane && mutedLanes.Contains(lane.Parent);
                bool muted = mutedByParent || mutedLanes.Contains(lane);

                muteButton.Enabled.Value = !mutedByParent;
                muteButton.Icon = muted ? FontAwesome.Solid.VolumeMute : FontAwesome.Solid.VolumeUp;
                muteButton.TooltipText = muted ? SlopHitsoundEditorStrings.UnmuteLane : SlopHitsoundEditorStrings.MuteLane;
                muteButton.IconColour = muted ? Colour4.Gray : Colour4.White;
            }
        }
    }
}
