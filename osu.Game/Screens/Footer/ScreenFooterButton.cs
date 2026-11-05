// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Footer
{
    public partial class ScreenFooterButton : OsuClickableContainer, IKeyBindingHandler<GlobalAction>, IScreenFooterButton
    {
        public const int CORNER_RADIUS = 10;

        public const int HEIGHT = 75;
        protected const int BUTTON_WIDTH = 116;

        /// <summary>
        /// The size of the clickable area of song select buttons in stable, which legacy skins design their artwork around.
        /// </summary>
        public static readonly Vector2 LEGACY_SIZE = new Vector2(77, 90);

        /// <summary>
        /// The legacy skin texture which replaces this button's visuals when the user's legacy skin provides it.
        /// The <c>-over</c> variant is shown on hover.
        /// </summary>
        protected virtual string? LegacyTextureName => null;

        /// <summary>
        /// Whether this button is currently displayed using legacy skin textures.
        /// </summary>
        public bool IsLegacy { get; private set; }

        /// <summary>
        /// The width of this button without any extra content, depending on whether it is displayed with legacy skin textures.
        /// </summary>
        protected float BaseWidth => IsLegacy ? LEGACY_SIZE.X : BUTTON_WIDTH;

        [Resolved]
        private ISkinSource? skin { get; set; }

        /// <summary>
        /// If this footer button controls the visibility of an overlay, it will be bound to this bindable.
        /// </summary>
        public readonly Bindable<Visibility> OverlayState = new Bindable<Visibility>();

        /// <summary>
        /// Used to tell whether this button is currently on screen.
        /// <see cref="ScreenFooter"/> controls this to convey when buttons are hidden temporarily via <see cref="ScreenFooter.RegisterActiveOverlayContainer"/>.
        /// </summary>
        public readonly Bindable<Visibility> State = new Bindable<Visibility>(Visibility.Visible);

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private Colour4 buttonAccentColour;

        public Colour4 AccentColour
        {
            set
            {
                buttonAccentColour = value;
                bar.Colour = buttonAccentColour;
                icon.Colour = buttonAccentColour;
            }
        }

        public IconUsage Icon
        {
            set => icon.Icon = value;
        }

        public LocalisableString Text
        {
            get => text.Text;
            set => text.Text = value;
        }

        public new Action? Action
        {
            set
            {
                if (value == null)
                    base.Action = null;
                else
                {
                    base.Action = () =>
                    {
                        if (State.Value != Visibility.Hidden)
                            value.Invoke();
                    };
                }
            }
        }

        private readonly Container shearedContent;

        private readonly SpriteText text;
        private readonly SpriteIcon icon;

        protected Container TextContainer;
        private readonly Box bar;
        private readonly Box backgroundBox;
        private readonly Box glowBox;
        private readonly Box flashLayer;

        private readonly Container legacyContent;
        private readonly Sprite legacySprite;
        private readonly Sprite legacyOverSprite;

        public readonly OverlayContainer? Overlay;

        public ScreenFooterButton(OverlayContainer? overlay = null)
        {
            Overlay = overlay;

            Size = new Vector2(BUTTON_WIDTH, HEIGHT);

            Children = new Drawable[]
            {
                shearedContent = new Container
                {
                    Shear = OsuGame.SHEAR,
                    Masking = true,
                    CornerRadius = CORNER_RADIUS,
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        backgroundBox = new Box
                        {
                            RelativeSizeAxes = Axes.Both
                        },
                        glowBox = new Box
                        {
                            RelativeSizeAxes = Axes.Both
                        },
                        // For elements that should not be sheared.
                        new Container
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Shear = -OsuGame.SHEAR,
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                TextContainer = new Container
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Y = 35,
                                    AutoSizeAxes = Axes.Both,
                                    Child = text = new OsuSpriteText
                                    {
                                        Font = OsuFont.TorusAlternate.With(size: 16),
                                        AlwaysPresent = true
                                    }
                                },
                                icon = new SpriteIcon
                                {
                                    Y = 10,
                                    Size = new Vector2(16),
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre
                                },
                            }
                        },
                        new Container
                        {
                            Shear = -OsuGame.SHEAR,
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.Centre,
                            Y = -CORNER_RADIUS,
                            Size = new Vector2(100, 5),
                            Masking = true,
                            CornerRadius = 3,
                            Child = bar = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                            }
                        },
                        flashLayer = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.White.Opacity(0.9f),
                            Blending = BlendingParameters.Additive,
                            Alpha = 0,
                        },
                    },
                },
                legacyContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    // the footer lets buttons extend below the screen edge by the corner radius.
                    Padding = new MarginPadding { Bottom = CORNER_RADIUS },
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        legacySprite = new Sprite
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                        },
                        legacyOverSprite = new Sprite
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Alpha = 0,
                        },
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (Overlay != null)
                OverlayState.BindTo(Overlay.State);

            OverlayState.BindValueChanged(_ => UpdateDisplay());
            Enabled.BindValueChanged(_ => UpdateDisplay(), true);

            if (skin != null)
                skin.SourceChanged += onSkinChanged;
            updateLegacyState();

            FinishTransforms(true);
        }

        private void onSkinChanged() => Schedule(updateLegacyState);

        private void updateLegacyState()
        {
            Texture? texture = LegacyTextureName == null ? null : skin?.GetLegacyLayoutTexture(LegacyTextureName);

            IsLegacy = texture != null;

            legacySprite.SetLegacyTexture(texture);
            // like stable, a missing hover texture falls back to the default legacy skin's.
            legacyOverSprite.SetLegacyTexture(IsLegacy ? skin!.GetTexture(LegacyTextureName + "-over") : null);

            // old layout skins draw both sprites top-aligned at stable's y=426, i.e. 54 stable units above the screen bottom.
            bool oldLayout = IsLegacy && skin!.UsesOldSongSelectButtonLayout();

            foreach (var sprite in new[] { legacySprite, legacyOverSprite })
            {
                sprite.Anchor = sprite.Origin = oldLayout ? Anchor.TopLeft : Anchor.BottomLeft;
                sprite.Y = oldLayout ? LEGACY_SIZE.Y - 54 * LegacySongSelectComponent.STABLE_SCALE : 0;
            }

            shearedContent.Alpha = IsLegacy ? 0 : 1;
            legacyContent.Alpha = IsLegacy ? 1 : 0;
            // drop pending width transforms from subclasses which were started for the previous layout.
            ClearTransforms(targetMember: nameof(Width));
            Size = new Vector2(BaseWidth, IsLegacy ? LEGACY_SIZE.Y + CORNER_RADIUS : HEIGHT);

            UpdateDisplay();
        }

        // account for shear and buttons temporarily hidden with DisappearToBottom.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => IsLegacy ? legacyContent.ReceivePositionalInputAt(screenSpacePos) : shearedContent.ReceivePositionalInputAt(screenSpacePos);

        public GlobalAction? Hotkey;

        protected override bool OnClick(ClickEvent e)
        {
            if (Enabled.Value)
                Flash();

            return base.OnClick(e);
        }

        protected virtual void Flash()
        {
            if (!IsLegacy)
                flashLayer.FadeOutFromOne(800, Easing.OutQuint);
        }

        protected override bool OnHover(HoverEvent e)
        {
            UpdateDisplay();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e) => UpdateDisplay();

        public virtual bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action != Hotkey || e.Repeat) return false;

            TriggerClick();
            return true;
        }

        public virtual void OnReleased(KeyBindingReleaseEvent<GlobalAction> e) { }

        public void UpdateDisplay()
        {
            if (IsLegacy)
            {
                // stable's HoverEffect: a linear 100ms fade, reversed when the hover is lost.
                legacyOverSprite.FadeTo(IsHovered || OverlayState.Value == Visibility.Visible ? 1 : 0, 100);
                legacySprite.FadeTo(Enabled.Value ? 1 : 0.5f, 150, Easing.OutQuint);
            }

            Color4 backgroundColour = OverlayState.Value == Visibility.Visible ? buttonAccentColour : colourProvider.Background3.Opacity(0.6f);
            Color4 textColour = OverlayState.Value == Visibility.Visible ? colourProvider.Background6 : colourProvider.Content1;
            Color4 accentColour = OverlayState.Value == Visibility.Visible ? colourProvider.Background6 : buttonAccentColour;

            if (!Enabled.Value)
                backgroundColour = backgroundColour.Darken(1f);
            else if (IsHovered)
                backgroundColour = backgroundColour.Lighten(0.2f);

            backgroundBox.FadeColour(backgroundColour, 150, Easing.OutQuint);

            if (!Enabled.Value)
                textColour = textColour.Opacity(0.6f);

            text.FadeColour(textColour, 150, Easing.OutQuint);
            icon.FadeColour(accentColour, 150, Easing.OutQuint);
            bar.FadeColour(accentColour, 150, Easing.OutQuint);

            glowBox.FadeColour(ColourInfo.GradientVertical(buttonAccentColour.Opacity(0f), buttonAccentColour.Opacity(0.2f)), 150, Easing.OutQuint);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin != null)
                skin.SourceChanged -= onSkinChanged;
        }

        public void AppearFromLeft(double delay)
        {
            Content.FinishTransforms();
            Content.MoveToX(-300f)
                   .FadeOut()
                   .Delay(delay)
                   .MoveToX(0f, 240, Easing.OutCubic)
                   .FadeIn(240, Easing.OutCubic);
        }

        public void AppearFromBottom(double delay)
        {
            Content.FinishTransforms();
            Content.MoveToY(100f)
                   .FadeOut()
                   .Delay(delay)
                   .MoveToY(0f, 240, Easing.OutCubic)
                   .FadeIn(240, Easing.OutCubic);
        }

        public void DisappearToRight(double delay, bool expire)
        {
            Content.FinishTransforms();
            Content.Delay(delay)
                   .FadeOut(240, Easing.InOutCubic)
                   .MoveToX(300f, 360, Easing.InOutCubic);

            if (expire)
                this.Delay(Content.LatestTransformEndTime - Time.Current).Expire();
        }

        public void DisappearToBottom(double delay, bool expire)
        {
            Content.FinishTransforms();
            Content.Delay(delay)
                   .FadeOut(240, Easing.InOutCubic)
                   .MoveToY(100f, 240, Easing.InOutCubic);

            if (expire)
                this.Delay(Content.LatestTransformEndTime - Time.Current).Expire();
        }
    }
}
