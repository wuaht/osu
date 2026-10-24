// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.Discord;
using osuTK;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// The list of guilds on the left side, with a button for the direct messages at the top.
    /// </summary>
    public partial class DiscordGuildRail : CompositeDrawable
    {
        private const float icon_size = 48;

        [Resolved]
        private DiscordClient client { get; set; } = null!;

        [Resolved]
        private DiscordNavigation navigation { get; set; } = null!;

        private readonly BindableList<DiscordGuild> guilds = new BindableList<DiscordGuild>();

        private FillFlowContainer guildFlow = null!;

        public DiscordGuildRail()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChild = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = false,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Padding = new MarginPadding { Vertical = 12 },
                    Children = new Drawable[]
                    {
                        new GuildButton(null, SlopDiscordStrings.DirectMessages),
                        new Box
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Size = new Vector2(32, 2),
                            Colour = colourProvider.Background3,
                        },
                        guildFlow = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 8),
                        },
                    },
                },
            };

            guilds.BindTo(client.Guilds);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            guilds.BindCollectionChanged((_, _) => Scheduler.AddOnce(updateGuilds), true);
        }

        private void updateGuilds()
        {
            guildFlow.Clear();

            foreach (var guild in guilds)
                guildFlow.Add(new GuildButton(guild, guild.Name));
        }

        private partial class GuildButton : OsuClickableContainer
        {
            private readonly DiscordGuild? guild;

            private Container iconContainer = null!;
            private Box iconBackground = null!;
            private Circle pill = null!;

            [Resolved]
            private DiscordNavigation navigation { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private readonly Bindable<DiscordGuild?> selectedGuild = new Bindable<DiscordGuild?>();

            private bool selected => selectedGuild.Value?.Id == guild?.Id;

            public GuildButton(DiscordGuild? guild, LocalisableString name)
            {
                this.guild = guild;

                RelativeSizeAxes = Axes.X;
                Height = icon_size;
                TooltipText = name;
            }

            [BackgroundDependencyLoader]
            private void load(TextureStore textures)
            {
                Drawable icon;

                if (guild == null)
                {
                    icon = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(0.6f),
                        FillMode = FillMode.Fit,
                        Texture = textures.Get(@"Slop/discord"),
                    };
                }
                else if (guild.GetIconUrl() is string url)
                {
                    icon = new DiscordRemoteImage(url)
                    {
                        RelativeSizeAxes = Axes.Both,
                        ShowPlaceholder = false,
                    };
                }
                else
                {
                    icon = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = guild.Acronym,
                        Font = DiscordStyle.UiFont(guild.Acronym.Length > 2 ? 14 : 17, FontWeight.SemiBold),
                        Colour = colourProvider.Content1,
                    };
                }

                Children = new Drawable[]
                {
                    pill = new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        Size = new Vector2(8, 0),
                        Colour = colourProvider.Content1,
                    },
                    iconContainer = new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(icon_size),
                        Masking = true,
                        CornerRadius = icon_size / 2,
                        Children = new[]
                        {
                            iconBackground = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourProvider.Background3,
                            },
                            icon,
                        },
                    },
                };

                selectedGuild.BindTo(navigation.Guild);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Action = () => navigation.Guild.Value = guild;

                selectedGuild.BindValueChanged(_ => updateState(), true);
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
                bool active = selected || IsHovered;

                iconContainer.TransformTo(nameof(CornerRadius), active ? 16f : icon_size / 2, 200, Easing.OutQuint);
                iconBackground.FadeColour(active ? DiscordStyle.Blurple : colourProvider.Background3, 200, Easing.OutQuint);

                float pillHeight = selected ? 40 : IsHovered ? 20 : 0;
                pill.ResizeHeightTo(pillHeight, 200, Easing.OutQuint);
            }
        }
    }
}
