// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Overlays.Login
{
    /// <summary>
    /// Asks whether the website should be signed into as well after the game was verified with a code from the email,
    /// and opens the website for signing in when confirmed.
    /// </summary>
    public partial class WebsiteLoginSuggestion : Component
    {
        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private WebSession? webSession;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            webSession = (api as APIAccess)?.WebSession;

            if (webSession != null)
                webSession.BrowserLoginSuggested += suggestLogin;
        }

        private void suggestLogin()
        {
            if (webSession == null || webSession.IsLoggedIn)
                return;

            var session = webSession;

            dialogOverlay?.Push(new WebsiteLoginDialog(() => Task.Run(async () =>
            {
                try
                {
                    await session.LoginWithBrowserAsync().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Schedule(() => notifications?.Post(new SimpleErrorNotification { Text = SlopSettingsStrings.WebsiteLoginFailed(e.Message) }));
                }
            })));
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (webSession != null)
                webSession.BrowserLoginSuggested -= suggestLogin;
        }

        private partial class WebsiteLoginDialog : PopupDialog
        {
            public WebsiteLoginDialog(Action onConfirm)
            {
                HeaderText = SlopSettingsStrings.WebsiteLoginPrompt;
                BodyText = SlopSettingsStrings.WebsiteLoginDescription;

                Icon = FontAwesome.Solid.Globe;

                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogOkButton
                    {
                        Text = SlopSettingsStrings.WebsiteLoginPromptConfirm,
                        Action = onConfirm
                    },
                    new PopupDialogCancelButton
                    {
                        Text = SlopSettingsStrings.WebsiteLoginPromptCancel,
                    },
                };
            }
        }
    }
}
