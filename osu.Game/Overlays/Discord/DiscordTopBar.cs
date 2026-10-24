// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Sections.Slop;
using osuTK;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// The top bar of the <see cref="DiscordOverlay"/>, which can be dragged to resize the overlay.
    /// </summary>
    public partial class DiscordTopBar : Container
    {
        public Drawable DragBar { get; private set; } = null!;

        private Drawable guildsButton = null!;

        private readonly BindableBool hasGuilds = new BindableBool();

        [Resolved]
        private SettingsOverlay? settings { get; set; }

        [Resolved]
        private DiscordLayout? layout { get; set; }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, TextureStore textures)
        {
            const float icon_height = 12;

            var icon = textures.Get(@"Slop/discord");

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5,
                    Alpha = 0.85f,
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.X,
                    RelativeSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Padding = new MarginPadding { Left = 16 },
                    Children = new Drawable[]
                    {
                        new Sprite
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Texture = icon,
                            // As high as the capital letters of the title, and centred on them.
                            Size = icon == null ? new Vector2(icon_height) : new Vector2(icon_height * icon.Width / (float)icon.Height, icon_height),
                            Y = -2,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = SlopDiscordStrings.Title,
                            Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold),
                            Margin = new MarginPadding { Bottom = 2f },
                        },
                    },
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4, 0),
                    Margin = new MarginPadding { Right = 12 },
                    Children = new Drawable[]
                    {
                        guildsButton = new ToggleButton(FontAwesome.Solid.Server, layout?.ShowGuilds)
                        {
                            TooltipText = SlopDiscordStrings.ToggleServers,
                        },
                        new ToggleButton(FontAwesome.Solid.Hashtag, layout?.ShowChannels)
                        {
                            TooltipText = SlopDiscordStrings.ToggleChannels,
                        },
                        new IconButton(FontAwesome.Solid.Cog, () => settings?.ShowAtControl<DiscordSettings>())
                        {
                            TooltipText = SlopDiscordStrings.OpenSettings,
                        },
                    },
                },
                DragBar = new DragArea
                {
                    Alpha = RuntimeInfo.IsMobile ? 1 : 0,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = colourProvider.Background1,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (layout != null)
                hasGuilds.BindTo(layout.HasGuilds);

            hasGuilds.BindValueChanged(h => guildsButton.Alpha = h.NewValue ? 1 : 0, true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (!RuntimeInfo.IsMobile)
                DragBar.FadeIn(100);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            if (!RuntimeInfo.IsMobile)
                DragBar.FadeOut(100);
            base.OnHoverLost(e);
        }

        private partial class IconButton : OsuClickableContainer
        {
            private readonly SpriteIcon spriteIcon;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public IconButton(IconUsage icon, System.Action action)
            {
                Size = new Vector2(28);
                Action = action;

                Child = spriteIcon = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = icon,
                    Size = new Vector2(16),
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                spriteIcon.Colour = colourProvider.Content2;
            }

            protected override bool OnHover(HoverEvent e)
            {
                spriteIcon.FadeColour(colourProvider.Content1, 100);
                spriteIcon.RotateTo(30, 400, Easing.OutElasticHalf);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                spriteIcon.FadeColour(colourProvider.Content2, 100);
                spriteIcon.RotateTo(0, 400, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }

        /// <summary>
        /// A button which shows or hides a side panel, and is dimmed while the panel is hidden.
        /// </summary>
        private partial class ToggleButton : OsuClickableContainer
        {
            private readonly SpriteIcon spriteIcon;

            private readonly BindableBool shown = new BindableBool(true);

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public ToggleButton(IconUsage icon, BindableBool? target)
            {
                Size = new Vector2(28);

                if (target != null)
                    shown.BindTo(target);

                Action = () => shown.Toggle();

                Child = spriteIcon = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = icon,
                    Size = new Vector2(14),
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                shown.BindValueChanged(_ => updateState(), true);
                FinishTransforms(true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateState();
                base.OnHoverLost(e);
            }

            private void updateState()
            {
                spriteIcon.FadeColour(IsHovered ? colourProvider.Content1 : colourProvider.Content2, 100);
                spriteIcon.FadeTo(shown.Value ? 1 : 0.4f, 100);
            }
        }

        private partial class DragArea : CompositeDrawable
        {
            private readonly Circle circle;

            private bool dragging;

            public DragArea()
            {
                AutoSizeAxes = Axes.Both;

                InternalChild = circle = new Circle
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(150, 7),
                    Margin = new MarginPadding(12),
                    Alpha = 0.6f,
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateScale();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateScale();
                base.OnHoverLost(e);
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                dragging = true;
                updateScale();
                return base.OnMouseDown(e);
            }

            protected override void OnMouseUp(MouseUpEvent e)
            {
                dragging = false;
                updateScale();
                base.OnMouseUp(e);
            }

            private void updateScale()
            {
                if (dragging || IsHovered)
                    circle.FadeIn(100);
                else
                    circle.FadeTo(0.6f, 100);

                if (dragging)
                    circle.ScaleTo(1f, 400, Easing.OutQuint);
                else if (IsHovered)
                    circle.ScaleTo(1.05f, 400, Easing.OutElasticHalf);
                else
                    circle.ScaleTo(1f, 500, Easing.OutQuint);
            }
        }
    }
}
