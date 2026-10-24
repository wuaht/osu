// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Localisation;
using osu.Game.Online.Discord;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Edit.Components;
using osuTK.Graphics;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// A Discord client in an overlay, which is anchored to the bottom of the screen like the chat overlay.
    /// </summary>
    public partial class DiscordOverlay : OsuFocusedOverlayContainer, INamedOverlayComponent
    {
        public IconUsage Icon => FontAwesome.Brands.Discord;
        public LocalisableString Title => SlopDiscordStrings.Title;
        public LocalisableString Description => SlopDiscordStrings.Description;

        public const float TOP_BAR_HEIGHT = 40;

        private const int transition_length = 500;

        protected override string PopInSampleName => @"UI/overlay-big-pop-in";
        protected override string PopOutSampleName => @"UI/overlay-big-pop-out";

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(DiscordStyle.HUE);

        [Cached]
        private readonly DiscordLayout layout = new DiscordLayout();

        [Cached]
        private readonly DiscordUnreadMessages unread = new DiscordUnreadMessages();

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private DiscordManager? discordManager { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private readonly BindableFloat overlayHeight = new BindableFloat();
        private readonly BindableFloat backgroundOpacity = new BindableFloat();
        private readonly BindableBool frosted = new BindableBool();

        private readonly IBindable<DiscordClient?> client = new Bindable<DiscordClient?>();
        private readonly Bindable<DiscordConnectionState> state = new Bindable<DiscordConnectionState>();

        private DiscordTopBar topBar = null!;
        private FrostedPanelBackground frostedBackground = null!;
        private Box background = null!;
        private Container<Drawable> viewContainer = null!;

        private Drawable? currentView;

        private DiscordTextureStore? textures;

        private bool isDraggingTopBar;
        private float dragStartHeight;

        public DiscordOverlay()
        {
            Height = 0.5f;

            Masking = true;

            const float corner_radius = 7f;

            CornerRadius = corner_radius;

            // Hide the bottom corners below the screen edge.
            Margin = new MarginPadding { Bottom = -corner_radius };
            Padding = new MarginPadding { Bottom = corner_radius };

            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.Cache(textures = new DiscordTextureStore(parent.Get<GameHost>()));
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativePositionAxes = Axes.Both;

            Children = new Drawable[]
            {
                frostedBackground = new FrostedPanelBackground
                {
                    Alpha = 0,
                },
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = TOP_BAR_HEIGHT },
                    Child = new PopoverContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new OsuContextMenuContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = viewContainer = new Container<Drawable>
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                        },
                    },
                },
                topBar = new DiscordTopBar
                {
                    RelativeSizeAxes = Axes.X,
                    Height = TOP_BAR_HEIGHT,
                },
            };

            config.BindWith(OsuSetting.SlopDiscordOverlayHeight, overlayHeight);
            config.BindWith(OsuSetting.SlopDiscordBackgroundOpacity, backgroundOpacity);
            config.BindWith(OsuSetting.SlopDiscordFrosted, frosted);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            overlayHeight.BindValueChanged(h => Height = h.NewValue, true);
            backgroundOpacity.BindValueChanged(_ => updateBackground());
            frosted.BindValueChanged(_ => updateBackground(), true);

            if (discordManager != null)
                client.BindTo(discordManager.Client);

            client.BindValueChanged(c =>
            {
                if (c.OldValue != null)
                    c.OldValue.MessageCreated -= onMessageCreated;

                if (c.NewValue != null)
                    c.NewValue.MessageCreated += onMessageCreated;

                unread.Clear();
                state.UnbindBindings();

                if (c.NewValue != null)
                    state.BindTo(c.NewValue.State);

                updateView();
            }, true);

            state.BindValueChanged(_ => updateView());
        }

        private void onMessageCreated(DiscordMessage message) => Schedule(() =>
        {
            var c = client.Value;

            if (c == null)
                return;

            // Replying (even from another Discord client) means that the conversation was read.
            if (message.Author.Id == c.LocalUser.Value?.Id)
            {
                unread.MarkRead(message.ChannelId);
                return;
            }

            var channel = c.PrivateChannels.FirstOrDefault(ch => ch.Id == message.ChannelId);

            // Like Discord, only direct messages notify.
            if (channel == null)
                return;

            var main = currentView as DiscordMainView;

            if (State.Value == Visibility.Visible && main?.Navigation.Channel.Value?.Id == channel.Id)
                return;

            unread.Add(channel.Id);

            if (notifications == null)
                return;

            notifications.Post(new SimpleNotification
            {
                Icon = FontAwesome.Brands.Discord,
                Text = string.IsNullOrEmpty(message.Content)
                    ? SlopDiscordStrings.AttachmentNotification(message.Author.DisplayName)
                    : SlopDiscordStrings.MessageNotification(message.Author.DisplayName, message.Content),
                Activated = () =>
                {
                    Show();

                    if (currentView is DiscordMainView view && view.Client == c)
                        view.Navigation.OpenChannel(channel, c);

                    return true;
                },
            });
        });

        protected override void Update()
        {
            base.Update();

            // The open conversation is read while the overlay is shown.
            if (State.Value == Visibility.Visible && currentView is DiscordMainView main && main.Navigation.Channel.Value is DiscordChannel open)
                unread.MarkRead(open.Id);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (client.Value != null)
                client.Value.MessageCreated -= onMessageCreated;

            base.Dispose(isDisposing);
            textures?.Dispose();
        }

        private void updateBackground()
        {
            frostedBackground.Alpha = frosted.Value ? 1 : 0;

            // The frosted backdrop is darkened by itself already, so the black layer can be lighter on top of it.
            background.Alpha = frosted.Value ? backgroundOpacity.Value * 0.5f : backgroundOpacity.Value;
        }

        private void updateView()
        {
            var c = client.Value;

            if (c == null)
            {
                showView(null);
                return;
            }

            // Keep showing the conversations while reconnecting.
            bool showMain = state.Value == DiscordConnectionState.Ready
                            || (state.Value == DiscordConnectionState.Connecting && currentView is DiscordMainView existing && existing.Client == c);

            if (showMain)
            {
                if (currentView is DiscordMainView main && main.Client == c)
                    return;

                showView(new DiscordMainView(c));
            }
            else
            {
                if (currentView is DiscordLoginView login && login.Client == c)
                    return;

                showView(new DiscordLoginView(c));
            }
        }

        private void showView(Drawable? view)
        {
            currentView?.FadeOut(200, Easing.OutQuint).Expire();
            currentView = view;

            if (view == null)
                return;

            view.RelativeSizeAxes = Axes.Both;
            view.Alpha = 0;
            viewContainer.Add(view);
            view.FadeIn(200, Easing.OutQuint);

            if (State.Value == Visibility.Visible && view is DiscordMainView main)
                main.TakeTextBoxFocus();
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            isDraggingTopBar = topBar.DragBar.IsHovered;
            return base.OnMouseDown(e);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (!isDraggingTopBar)
                return base.OnDragStart(e);

            dragStartHeight = overlayHeight.Value;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (!isDraggingTopBar)
                return;

            // The setting is clamped to its range already.
            overlayHeight.Value = dragStartHeight - (e.MousePosition.Y - e.MouseDownPosition.Y) / Parent!.DrawSize.Y;
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            isDraggingTopBar = false;
            base.OnDragEnd(e);
        }

        protected override void PopIn()
        {
            this.MoveToY(0, transition_length, Easing.OutQuint);
            this.FadeIn(transition_length, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            base.PopOut();

            this.MoveToY(Height, transition_length, Easing.InSine);
            this.FadeOut(transition_length, Easing.InSine);

            if (currentView is DiscordMainView main)
                main.KillTextBoxFocus();
        }

        protected override void OnFocus(FocusEvent e)
        {
            if (currentView is DiscordMainView main)
                main.TakeTextBoxFocus();

            base.OnFocus(e);
        }
    }
}
