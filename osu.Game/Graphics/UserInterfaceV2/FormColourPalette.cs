// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using SlopEditorStrings = osu.Game.Localisation.SlopEditorStrings;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.UserInterfaceV2
{
    public partial class FormColourPalette : CompositeDrawable
    {
        public LocalisableString PaletteHeaderText { get; init; } = string.Empty;

        public BindableList<Colour4> Colours { get; } = new BindableList<Colour4>();

        /// <summary>
        /// Optional colours offered as presets in each colour picker popover.
        /// </summary>
        public BindableList<Colour4> Suggestions { get; } = new BindableList<Colour4>();

        public LocalisableString Caption { get; init; }
        public LocalisableString HintText { get; init; }

        public BindableBool CanAdd { get; } = new BindableBool(true);

        /// <summary>
        /// Whether colours can be locked via their context menu, see <see cref="IsLocked"/>.
        /// </summary>
        public bool AllowLocking { get; init; }

        /// <summary>
        /// The lock state of each colour in <see cref="Colours"/>, which is kept aligned with additions and removals of colours.
        /// </summary>
        private readonly List<bool> lockedStates = new List<bool>();

        private readonly List<ColourButton> colourButtons = new List<ColourButton>();

        /// <summary>
        /// Whether the colour at the given index has been locked by the user.
        /// </summary>
        public bool IsLocked(int index) => index >= 0 && index < lockedStates.Count && lockedStates[index];

        /// <summary>
        /// Sets the lock state of all colours.
        /// </summary>
        public void SetAllLocked(bool locked)
        {
            for (int i = 0; i < lockedStates.Count; i++)
                lockedStates[i] = locked;

            foreach (var button in colourButtons)
                button.UpdateLockState();
        }

        private FormControlBackground background = null!;
        private FormFieldCaption caption = null!;
        private FillFlowContainer flow = null!;
        private RoundedButton addButton = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChildren = new Drawable[]
            {
                background = new FormControlBackground(),
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(9),
                    Spacing = new Vector2(7),
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        caption = new FormFieldCaption
                        {
                            Caption = Caption,
                            TooltipText = HintText,
                        },
                        flow = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Full,
                            Spacing = new Vector2(5),
                            Child = addButton = new RoundedButton
                            {
                                Action = addNewColour,
                                Size = new Vector2(70),
                                Text = "+",
                            }
                        }
                    },
                },
            };

            flow.SetLayoutPosition(addButton, float.MaxValue);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Colours.BindCollectionChanged((_, args) =>
            {
                updateLockedStates(args);

                if (args.Action == NotifyCollectionChangedAction.Replace)
                {
                    // the buttons are kept, but have to display colours which were replaced externally.
                    for (int i = 0; i < args.NewItems!.Count; i++)
                    {
                        int index = args.NewStartingIndex + i;

                        if (index < colourButtons.Count)
                            colourButtons[index].Current.Value = Colours[index];
                    }
                }
                else
                    updateColours();
            }, true);
            CanAdd.BindValueChanged(canAdd =>
            {
                if (canAdd.NewValue)
                {
                    addButton.Enabled.Value = true;
                    addButton.TooltipText = string.Empty;
                }
                else
                {
                    addButton.Enabled.Value = false;
                    addButton.TooltipText = "Maximum combo colours reached";
                }
            }, true);
            updateState();
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            updateState();
        }

        private void addNewColour()
        {
            Color4 startingColour = Colours.Count > 0
                ? Colours.Last()
                : Colour4.White;

            Colours.Add(startingColour);
            flow.OfType<ColourButton>().Last().TriggerClick();
        }

        private void updateState()
        {
            caption.Colour = colourProvider.Content2;

            if (IsHovered)
                background.VisualStyle = VisualStyle.Hovered;
            else
                background.VisualStyle = VisualStyle.Normal;
        }

        private void updateLockedStates(NotifyCollectionChangedEventArgs args)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    // the initial notification of the bound list has no starting index.
                    int insertIndex = args.NewStartingIndex < 0 ? lockedStates.Count : Math.Min(args.NewStartingIndex, lockedStates.Count);
                    lockedStates.InsertRange(insertIndex, Enumerable.Repeat(false, args.NewItems!.Count));
                    break;

                case NotifyCollectionChangedAction.Remove when args.OldStartingIndex >= 0 && args.OldStartingIndex < lockedStates.Count:
                    lockedStates.RemoveRange(args.OldStartingIndex, Math.Min(args.OldItems!.Count, lockedStates.Count - args.OldStartingIndex));
                    break;

                case NotifyCollectionChangedAction.Move when args.OldStartingIndex >= 0 && args.OldStartingIndex < lockedStates.Count && args.NewStartingIndex >= 0:
                    bool movedState = lockedStates[args.OldStartingIndex];
                    lockedStates.RemoveAt(args.OldStartingIndex);
                    lockedStates.Insert(Math.Min(args.NewStartingIndex, lockedStates.Count), movedState);
                    break;
            }

            // keep the states aligned in all other cases (e.g. resets), keeping the states of the remaining colours.
            if (lockedStates.Count > Colours.Count)
                lockedStates.RemoveRange(Colours.Count, lockedStates.Count - Colours.Count);
            while (lockedStates.Count < Colours.Count)
                lockedStates.Add(false);
        }

        private void updateColours()
        {
            flow.RemoveAll(d => d is ColourButton, true);
            colourButtons.Clear();

            for (int i = 0; i < Colours.Count; ++i)
            {
                // copy to avoid accesses to modified closure.
                int colourIndex = i;
                var colourButton = new ColourButton(Suggestions)
                {
                    PaletteHeaderText = PaletteHeaderText,
                    Current = { Value = Colours[colourIndex] }
                };
                colourButton.Current.BindValueChanged(colour =>
                {
                    if (Colours[colourIndex] != colour.NewValue)
                        Colours[colourIndex] = colour.NewValue;
                });
                colourButton.DeleteRequested = () => Colours.RemoveAt(colourIndex);

                if (AllowLocking)
                {
                    colourButton.IsLocked = () => IsLocked(colourIndex);
                    colourButton.ToggleLockRequested = () =>
                    {
                        lockedStates[colourIndex] = !lockedStates[colourIndex];
                        colourButton.UpdateLockState();
                    };
                    colourButton.SetAllLockedRequested = SetAllLocked;
                }

                colourButtons.Add(colourButton);
                flow.Add(colourButton);
            }
        }

        private partial class ColourButton : OsuClickableContainer, IHasPopover, IHasContextMenu
        {
            public LocalisableString PaletteHeaderText { get; init; } = string.Empty;

            public Bindable<Colour4> Current { get; } = new Bindable<Colour4>();
            public Action? DeleteRequested { get; set; }

            /// <summary>
            /// Returns whether this colour is locked. Null if locking isn't supported.
            /// </summary>
            public Func<bool>? IsLocked { get; set; }

            public Action? ToggleLockRequested { get; set; }

            public Action<bool>? SetAllLockedRequested { get; set; }

            private SpriteIcon lockIcon = null!;

            private readonly BindableList<Colour4> suggestions;

            private Box background = null!;
            private OsuSpriteText hexCode = null!;

            public ColourButton(BindableList<Colour4> suggestions)
            {
                this.suggestions = suggestions;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Size = new Vector2(70);

                Masking = true;
                CornerRadius = 10;
                CornerExponent = 2.5f;
                Action = this.ShowPopover;

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    hexCode = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                    lockIcon = new SpriteIcon
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Margin = new MarginPadding(7),
                        Size = new Vector2(12),
                        Icon = FontAwesome.Solid.Lock,
                        Alpha = 0,
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Current.BindValueChanged(_ => updateState(), true);
                UpdateLockState();
            }

            public void UpdateLockState()
            {
                if (IsLoaded)
                    lockIcon.Alpha = IsLocked?.Invoke() == true ? 1 : 0;
            }

            public Popover GetPopover() => new ColourPickerPopover(suggestions)
            {
                PaletteHeaderText = PaletteHeaderText,
                Current = { BindTarget = Current }
            };

            public MenuItem[] ContextMenuItems
            {
                get
                {
                    var items = new List<MenuItem>();

                    if (IsLocked != null)
                    {
                        items.Add(new OsuMenuItem(IsLocked() ? SlopEditorStrings.UnlockColour : SlopEditorStrings.LockColour, MenuItemType.Standard, () => ToggleLockRequested?.Invoke()));
                        items.Add(new OsuMenuItem(SlopEditorStrings.LockAllColours, MenuItemType.Standard, () => SetAllLockedRequested?.Invoke(true)));
                        items.Add(new OsuMenuItem(SlopEditorStrings.UnlockAllColours, MenuItemType.Standard, () => SetAllLockedRequested?.Invoke(false)));
                    }

                    items.Add(new OsuMenuItem(CommonStrings.ButtonsDelete, MenuItemType.Destructive, () => DeleteRequested?.Invoke()));

                    return items.ToArray();
                }
            }

            private void updateState()
            {
                background.Colour = Current.Value;
                hexCode.Text = Current.Value.ToHex();
                hexCode.Colour = lockIcon.Colour = OsuColour.ForegroundTextColourFor(Current.Value);
            }
        }

        private partial class ColourPickerPopover : OsuPopover, IHasCurrentValue<Colour4>
        {
            public LocalisableString PaletteHeaderText { get; init; } = string.Empty;

            public Bindable<Colour4> Current
            {
                get => current.Current;
                set => current.Current = value;
            }

            private readonly BindableWithCurrent<Colour4> current = new BindableWithCurrent<Colour4>();
            private readonly BindableList<Colour4> suggestions;

            public ColourPickerPopover(BindableList<Colour4> suggestions)
                : base(false)
            {
                this.suggestions = suggestions;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                var picker = new OsuColourPicker
                {
                    PaletteHeaderText = PaletteHeaderText,
                    Current = { BindTarget = Current },
                };
                picker.Suggestions.BindTo(suggestions);
                Child = picker;

                Body.BorderThickness = 2;
                Body.BorderColour = colourProvider.Highlight1;
                Content.Padding = new MarginPadding(2);
            }
        }
    }
}
