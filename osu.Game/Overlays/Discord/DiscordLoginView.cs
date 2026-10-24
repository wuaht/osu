// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.Discord;
using osu.Game.Online.Discord.Full;
using osu.Game.Overlays.Settings.Sections.Slop;
using osuTK;
using osuTK.Graphics;
using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// Shows how to log in, and the progress of a login.
    /// </summary>
    public partial class DiscordLoginView : CompositeDrawable
    {
        private const float content_width = 360;

        public readonly DiscordClient Client;

        private readonly Bindable<DiscordConnectionState> state = new Bindable<DiscordConnectionState>();
        private readonly Bindable<string?> error = new Bindable<string?>();

        private readonly Bindable<string?> qrCodeUrl = new Bindable<string?>();
        private readonly Bindable<DiscordUser?> scannedUser = new Bindable<DiscordUser?>();
        private readonly BindableBool qrCodeExpired = new BindableBool();

        private FillFlowContainer content = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private SettingsOverlay? settings { get; set; }

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private Texture? qrCodeTexture;

        public DiscordLoginView(DiscordClient client)
        {
            Client = client;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(30),
                    Child = content = new FillFlowContainer
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Width = content_width,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 14),
                    },
                },
            };

            state.BindTo(Client.State);
            error.BindTo(Client.Error);

            if (Client is DiscordFullClient full)
            {
                qrCodeUrl.BindTo(full.QrCodeUrl);
                scannedUser.BindTo(full.ScannedUser);
                qrCodeExpired.BindTo(full.QrCodeExpired);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            state.BindValueChanged(_ => Scheduler.AddOnce(updateContent));
            error.BindValueChanged(_ => Scheduler.AddOnce(updateContent));
            qrCodeUrl.BindValueChanged(_ => Scheduler.AddOnce(updateContent));
            scannedUser.BindValueChanged(_ => Scheduler.AddOnce(updateContent));
            qrCodeExpired.BindValueChanged(_ => Scheduler.AddOnce(updateContent));

            updateContent();
        }

        private bool isFullClient => Client is DiscordFullClient;

        private void updateContent()
        {
            content.Clear();

            content.Add(new Sprite
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Size = new Vector2(64),
                FillMode = FillMode.Fit,
                Texture = textures?.Get(@"Slop/discord"),
            });

            switch (state.Value)
            {
                case DiscordConnectionState.NotConfigured:
                    addTitle(SlopDiscordStrings.Title);
                    addText(error.Value ?? string.Empty, DiscordStyle.Error);
                    addButton(SlopDiscordStrings.OpenSettings, () => settings?.ShowAtControl<DiscordSettings>());
                    break;

                case DiscordConnectionState.LoggedOut:
                    addTitle(SlopDiscordStrings.Title);

                    if (isFullClient)
                    {
                        addWarning(SlopDiscordStrings.FullClientWarning.ToString());
                        addButton(SlopDiscordStrings.LoginWithQrCode, Client.Login);
                    }
                    else
                    {
                        addText(SlopDiscordStrings.LoginBrowserDescription.ToString(), colourProvider.Content2);
                        addButton(SlopDiscordStrings.LoginWithDiscord, Client.Login);
                    }

                    if (!string.IsNullOrEmpty(error.Value))
                        addText(error.Value, DiscordStyle.Error);

                    break;

                case DiscordConnectionState.LoggingIn:
                    if (isFullClient)
                        addQrCodeContent();
                    else
                    {
                        addTitle(SlopDiscordStrings.WaitingForBrowser);
                        addSpinner();
                        addButton(SlopDiscordStrings.Cancel, Client.CancelLogin, false);
                    }

                    break;

                default:
                    addTitle(SlopDiscordStrings.Connecting);
                    addSpinner();
                    break;
            }
        }

        [Resolved]
        private TextureStore? textures { get; set; }

        private void addQrCodeContent()
        {
            if (scannedUser.Value is DiscordUser user)
            {
                content.Add(new DiscordAvatar(user.GetAvatarUrl(128), 96)
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                });
                addTitle(SlopDiscordStrings.CheckYourPhone);
                addText(SlopDiscordStrings.LoggingInAs(user.DisplayName).ToString(), colourProvider.Content2);
                addButton(SlopDiscordStrings.Cancel, Client.CancelLogin, false);
                return;
            }

            addTitle(SlopDiscordStrings.LoginWithQrCode);
            addText(SlopDiscordStrings.ScanQrCode.ToString(), colourProvider.Content2);

            var card = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Size = new Vector2(196),
                Masking = true,
                CornerRadius = 10,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                    },
                },
            };

            content.Add(card);

            if (qrCodeExpired.Value)
            {
                card.Add(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5,
                    Alpha = 0.9f,
                });
                card.Add(new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = FontAwesome.Solid.Redo,
                    Size = new Vector2(32),
                    Colour = colourProvider.Content1,
                });

                addButton(SlopDiscordStrings.Retry, Client.Login);
                addButton(SlopDiscordStrings.Cancel, Client.CancelLogin, false);
                return;
            }

            var texture = string.IsNullOrEmpty(qrCodeUrl.Value) ? null : createQrCodeTexture(qrCodeUrl.Value);

            if (texture == null)
                card.Add(new LoadingSpinner
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = Color4.Black,
                    State = { Value = Visibility.Visible },
                });
            else
            {
                card.Add(new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(6),
                    Child = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = texture,
                    },
                });
            }

            addButton(SlopDiscordStrings.Cancel, Client.CancelLogin, false);
        }

        private string? lastQrCodeText;

        private Texture? createQrCodeTexture(string text)
        {
            if (text == lastQrCodeText && qrCodeTexture != null)
                return qrCodeTexture;

            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);

            var matrix = data.ModuleMatrix;
            int size = matrix.Count;

            // The module matrix includes the quiet zone already.
            using var image = new Image<Rgba32>(size, size);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    image[x, y] = matrix[y][x] ? new Rgba32(0, 0, 0, 255) : new Rgba32(255, 255, 255, 255);
            }

            qrCodeTexture?.Dispose();
            qrCodeTexture = renderer.CreateTexture(size, size, filteringMode: osu.Framework.Graphics.Textures.TextureFilteringMode.Nearest);
            qrCodeTexture.SetData(new TextureUpload(image.Clone()));

            lastQrCodeText = text;
            return qrCodeTexture;
        }

        private void addTitle(osu.Framework.Localisation.LocalisableString text)
        {
            content.Add(new OsuSpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Text = text,
                Font = DiscordStyle.UiFont(22, FontWeight.Bold),
                Colour = colourProvider.Content1,
            });
        }

        private void addText(string text, Color4 colour)
        {
            content.Add(new OsuTextFlowContainer(t =>
            {
                t.Font = DiscordStyle.UiFont(15);
                t.Colour = colour;
            })
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                TextAnchor = Anchor.TopCentre,
                Text = text,
            });
        }

        private void addWarning(string text)
        {
            content.Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 6,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = DiscordStyle.MentionedMessageBar,
                        Alpha = 0.15f,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 3,
                        Colour = DiscordStyle.MentionedMessageBar,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Vertical = 10, Left = 14, Right = 12 },
                        ColumnDimensions = new[] { new Dimension(GridSizeMode.AutoSize), new Dimension() },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new SpriteIcon
                                {
                                    Icon = FontAwesome.Solid.ExclamationTriangle,
                                    Size = new Vector2(16),
                                    Colour = DiscordStyle.MentionedMessageBar,
                                    Margin = new MarginPadding { Right = 10, Top = 1 },
                                },
                                new OsuTextFlowContainer(t =>
                                {
                                    t.Font = DiscordStyle.UiFont(14);
                                    t.Colour = colourProvider.Content1;
                                })
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Text = text,
                                },
                            },
                        },
                    },
                },
            });
        }

        private void addButton(osu.Framework.Localisation.LocalisableString text, System.Action action, bool primary = true)
        {
            var button = new RoundedButton
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Width = 240,
                Text = text,
                Action = action,
            };

            if (primary)
                button.BackgroundColour = DiscordStyle.Blurple;

            content.Add(button);
        }

        private void addSpinner()
        {
            content.Add(new LoadingSpinner
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Size = new Vector2(48),
                State = { Value = Visibility.Visible },
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            qrCodeTexture?.Dispose();
        }
    }
}
