// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osuTK;
using osuTK.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Screens.Edit.Components
{
    public partial class PlaybackControl : BottomBarContainer
    {
        private IconButton playButton = null!;
        private PlaybackSpeedControl playbackSpeedControl = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private readonly Bindable<EditorScreenMode> currentScreenMode = new Bindable<EditorScreenMode>();
        /// <summary>
        /// The playback speed selected by the user.
        /// </summary>
        private readonly BindableNumber<double> playbackSpeed = new BindableDouble(1);

        private readonly BindableNumber<double> tempoAdjustment = new BindableDouble(1);

        /// <summary>
        /// Used instead of <see cref="tempoAdjustment"/> in the timing screen.
        /// Changing the speed together with the pitch (resampling) keeps transients sharp and doesn't delay the audio,
        /// unlike changing the tempo (time stretching), which made timing at slower speeds inaccurate.
        /// </summary>
        private readonly BindableNumber<double> frequencyAdjustment = new BindableDouble(1);

        private readonly Bindable<bool> noAudioStretch = new Bindable<bool>();

        /// <summary>
        /// The rate of the mods kept for previewing in the editor (see <see cref="OsuSetting.SlopEditorPreviewMods"/>).
        /// </summary>
        private double modRate = 1;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, Editor? editor, OsuConfigManager? config, IBindable<IReadOnlyList<Mod>>? mods)
        {
            config?.BindWith(OsuSetting.SlopEditorNoAudioStretch, noAudioStretch);

            if (mods != null)
                modRate = mods.Value.OfType<ModRateAdjust>().Aggregate(1.0, (rate, mod) => rate * mod.SpeedChange.Value);

            Background.Colour = EditorPanelStyle.PanelBackground;

            Children = new Drawable[]
            {
                playButton = new IconButton
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    IconScale = new Vector2(1.2f),
                    Icon = FontAwesome.Regular.PlayCircle,
                    Action = togglePause,
                },
                playbackSpeedControl = new PlaybackSpeedControl
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Padding = new MarginPadding { Left = 36, },
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = EditorStrings.PlaybackSpeed,
                            Font = OsuFont.GetFont(size: 13),
                        },
                        new PlaybackTabControl
                        {
                            Current = playbackSpeed,
                            RelativeSizeAxes = Axes.X,
                            Height = 14,
                        },
                    }
                }
            };

            editorClock.AudioAdjustments.AddAdjustment(AdjustableProperty.Tempo, tempoAdjustment);
            editorClock.AudioAdjustments.AddAdjustment(AdjustableProperty.Frequency, frequencyAdjustment);

            if (editor != null)
            {
                currentScreenMode.BindTo(editor.Mode);
                playbackSpeed.BindTo(editor.PlaybackSpeed);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentScreenMode.BindValueChanged(_ => updateSpeedAdjustments());
            noAudioStretch.BindValueChanged(_ => updateSpeedAdjustments());
            playbackSpeed.BindValueChanged(_ => updateSpeedAdjustments(), true);
        }

        private void updateSpeedAdjustments()
        {
            bool isTimingScreen = currentScreenMode.Value == EditorScreenMode.Timing;
            bool changePitch = isTimingScreen || noAudioStretch.Value;
            double speed = playbackSpeed.Value * modRate;

            tempoAdjustment.Value = changePitch ? 1 : speed;
            frequencyAdjustment.Value = changePitch ? speed : 1;

            if (noAudioStretch.Value)
                playbackSpeedControl.TooltipText = "The pitch changes along with the speed. This keeps the audio sharp and in sync, so timing stays accurate at slower speeds.";
            else if (isTimingScreen)
                playbackSpeedControl.TooltipText = "In timing mode, the pitch changes along with the speed. This keeps the audio sharp and in sync, so timing stays accurate at slower speeds.";
            else
                playbackSpeedControl.TooltipText = default(LocalisableString);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (editorClock.IsNotNull())
            {
                editorClock.AudioAdjustments.RemoveAdjustment(AdjustableProperty.Tempo, tempoAdjustment);
                editorClock.AudioAdjustments.RemoveAdjustment(AdjustableProperty.Frequency, frequencyAdjustment);
            }

            base.Dispose(isDisposing);
        }

        private void togglePause()
        {
            if (editorClock.IsRunning)
                editorClock.Stop();
            else
                editorClock.Start();
        }

        private static readonly IconUsage play_icon = FontAwesome.Regular.PlayCircle;
        private static readonly IconUsage pause_icon = FontAwesome.Regular.PauseCircle;

        protected override void Update()
        {
            base.Update();

            playButton.Icon = editorClock.IsRunning ? pause_icon : play_icon;
        }

        private partial class PlaybackSpeedControl : FillFlowContainer, IHasTooltip
        {
            public LocalisableString TooltipText { get; set; }
        }

        public partial class PlaybackTabControl : OsuTabControl<double>
        {
            private static readonly double[] tempo_values = { 0.25, 0.5, 0.75, 1, 1.5 };

            protected override TabItem<double> CreateTabItem(double value) => new PlaybackTabItem(value);

            protected override Dropdown<double> CreateDropdown() => null!;

            public PlaybackTabControl()
            {
                RelativeSizeAxes = Axes.Both;
                TabContainer.Spacing = Vector2.Zero;

                tempo_values.ForEach(AddItem);

                Current.Value = 1;
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                // the values are spread with equal gaps between them, from the left edge (lined up with the label above) to the right edge.
                var items = TabContainer.Children;
                float itemsWidth = items.Sum(i => i.DrawWidth);
                float spacing = items.Count > 1 ? Math.Max(0, (DrawWidth - itemsWidth) / (items.Count - 1)) : 0;

                if (TabContainer.Spacing.X != spacing)
                    TabContainer.Spacing = new Vector2(spacing, 0);
            }

            public partial class PlaybackTabItem : TabItem<double>
            {
                private const float fade_duration = 200;

                private readonly OsuSpriteText text;
                private readonly OsuSpriteText textBold;

                public PlaybackTabItem(double value)
                    : base(value)
                {
                    RelativeSizeAxes = Axes.Y;
                    AutoSizeAxes = Axes.X;

                    Children = new Drawable[]
                    {
                        text = new OsuSpriteText
                        {
                            Origin = Anchor.TopLeft,
                            Anchor = Anchor.TopLeft,
                            Text = $"{value:0%}",
                            Font = OsuFont.GetFont(size: 12)
                        },
                        textBold = new OsuSpriteText
                        {
                            Origin = Anchor.TopLeft,
                            Anchor = Anchor.TopLeft,
                            Text = $"{value:0%}",
                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                            Alpha = 0,
                        },
                    };
                }

                private Color4 hoveredColour;
                private Color4 normalColour;

                [BackgroundDependencyLoader]
                private void load(OverlayColourProvider colourProvider)
                {
                    text.Colour = normalColour = colourProvider.Light3;
                    textBold.Colour = hoveredColour = colourProvider.Content1;
                }

                protected override bool OnHover(HoverEvent e)
                {
                    updateState();
                    return false;
                }

                protected override void OnHoverLost(HoverLostEvent e) => updateState();
                protected override void OnActivated() => updateState();
                protected override void OnDeactivated() => updateState();

                private void updateState()
                {
                    text.FadeColour(Active.Value || IsHovered ? hoveredColour : normalColour, fade_duration, Easing.OutQuint);
                    text.FadeTo(Active.Value ? 0 : 1, fade_duration, Easing.OutQuint);
                    textBold.FadeTo(Active.Value ? 1 : 0, fade_duration, Easing.OutQuint);
                }
            }
        }
    }
}
