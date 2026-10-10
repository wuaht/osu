// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.BnTracker;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings.Sections.Slop;
using osuTK;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// What is shown instead of the nominators while they can't be shown, e.g. while signing in or loading.
    /// </summary>
    public partial class NominatorsStateDisplay : CompositeDrawable
    {
        [Resolved]
        private NominatorsSession session { get; set; } = null!;

        [Resolved]
        private BnTrackerClient client { get; set; } = null!;

        [Resolved]
        private Clipboard clipboard { get; set; } = null!;

        [Resolved]
        private SettingsOverlay? settingsOverlay { get; set; }

        private readonly IBindable<NominatorsSessionState> state = new Bindable<NominatorsSessionState>();

        private FillFlowContainer content = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = new NominatorsPanel(Axes.Y)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 520,
                Child = content = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 12),
                    Padding = new MarginPadding(30),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            state.BindTo(session.State);
            state.BindValueChanged(_ => updateDisplay(), true);
        }

        private void updateDisplay()
        {
            content.Clear();

            switch (state.Value)
            {
                case NominatorsSessionState.NotSubmitted:
                    add(FontAwesome.Solid.CloudUploadAlt, SlopNominatorsStrings.NotSubmittedTitle, SlopNominatorsStrings.NotSubmittedDescription);
                    break;

                case NominatorsSessionState.NoServer:
                    add(FontAwesome.Solid.Server, SlopNominatorsStrings.NoServerTitle, SlopNominatorsStrings.NoServerDescription);
                    addButton(SlopNominatorsStrings.OpenSettings, () => settingsOverlay?.ShowAtControl<OnlineSettings>());
                    break;

                case NominatorsSessionState.SignedOut:
                    add(FontAwesome.Solid.SignInAlt, SlopNominatorsStrings.SignedOutTitle,
                        client.CanSignInWithOsu ? SlopNominatorsStrings.SignedOutDescription : SlopNominatorsStrings.SignedOutOfflineDescription);
                    addMessage();

                    if (client.CanSignInWithOsu)
                        addButton(SlopNominatorsStrings.SignInWithOsu, () => session.SignInWithOsu());

                    addButton(SlopNominatorsStrings.SignInWithBrowser, () => session.SignInWithBrowser());
                    break;

                case NominatorsSessionState.SigningIn:
                    add(FontAwesome.Solid.SignInAlt, SlopNominatorsStrings.SigningIn, default);
                    addSpinner();
                    break;

                case NominatorsSessionState.SigningInWithBrowser:
                    add(FontAwesome.Solid.Globe, SlopNominatorsStrings.SignInWithBrowserTitle, SlopNominatorsStrings.SignInWithBrowserDescription);

                    if (session.DeviceSignIn is BnDeviceSignIn signIn)
                    {
                        content.Add(new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = signIn.UserCode,
                            Font = OsuFont.Default.With(size: 40, weight: FontWeight.Bold, fixedWidth: true),
                        });

                        addButton(SlopNominatorsStrings.OpenBrowserAgain, () => session.OpenDeviceSignInPage());
                        addButton(SlopNominatorsStrings.CopyLink, () => clipboard.SetText(signIn.VerificationUrlComplete));
                    }

                    addSpinner();
                    addButton(SlopNominatorsStrings.Cancel, () => session.CancelSignIn());
                    break;

                case NominatorsSessionState.Loading:
                    add(FontAwesome.Solid.Users, SlopNominatorsStrings.Loading, default);
                    addSpinner();
                    break;

                case NominatorsSessionState.Failed:
                    add(FontAwesome.Solid.ExclamationTriangle, SlopNominatorsStrings.LoadingFailed, default);
                    addMessage();
                    addButton(SlopNominatorsStrings.Retry, () => session.Refresh());
                    break;

                case NominatorsSessionState.NotOwned:
                    add(FontAwesome.Solid.UserLock, SlopNominatorsStrings.NotOwnedTitle,
                        session.OwnerName != null ? SlopNominatorsStrings.NotOwnedDescription(session.OwnerName) : SlopNominatorsStrings.NotOwnedDescriptionUnknown);
                    addButton(SlopNominatorsStrings.Retry, () => session.Refresh());
                    break;
            }
        }

        private void add(IconUsage icon, LocalisableString title, LocalisableString description)
        {
            content.Add(new SpriteIcon
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Icon = icon,
                Size = new Vector2(36),
            });

            content.Add(new OsuSpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Text = title,
                Font = OsuFont.Default.With(size: 22, weight: FontWeight.Bold),
            });

            if (description != default)
                addText(description, 1);
        }

        private void addMessage()
        {
            if (!string.IsNullOrEmpty(session.Message))
                addText(session.Message, 0.7f);
        }

        private void addText(LocalisableString text, float alpha)
        {
            content.Add(new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 15))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                TextAnchor = Anchor.TopCentre,
                Text = text,
                Alpha = alpha,
            });
        }

        private void addSpinner() => content.Add(new LoadingSpinner
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Size = new Vector2(30),
            State = { Value = Visibility.Visible },
        });

        private void addButton(LocalisableString text, Action action) => content.Add(new RoundedButton
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Width = 260,
            Height = 36,
            Text = text,
            Action = action,
        });
    }
}
