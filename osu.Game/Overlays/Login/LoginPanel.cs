// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.OfflineProfiles;
using osu.Game.Overlays.OfflineProfiles;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Slop;
using osu.Game.Users;
using osuTK;

namespace osu.Game.Overlays.Login
{
    public partial class LoginPanel : Container
    {
        private bool bounding = true;

        private Drawable? form;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private UserDropdown? dropdown;

        /// <summary>
        /// Called to request a hide of a parent displaying this container.
        /// </summary>
        public Action? RequestHide;

        private readonly IBindable<APIState> apiState = new Bindable<APIState>();
        private readonly Bindable<UserStatus> configUserStatus = new Bindable<UserStatus>();
        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();
        private readonly IBindable<OfflineProfile?> activeOfflineProfile = new Bindable<OfflineProfile?>();

        [Resolved]
        private OfflineProfileManager? offlineProfiles { get; set; }

        [Resolved]
        private SettingsOverlay? settings { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        public override RectangleF BoundingBox => bounding ? base.BoundingBox : RectangleF.Empty;

        public bool Bounding
        {
            get => bounding;
            set
            {
                bounding = value;
                Invalidate(Invalidation.MiscGeometry);
            }
        }

        public LoginPanel()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            config.BindWith(OsuSetting.UserOnlineStatus, configUserStatus);
            configUserStatus.BindValueChanged(e => updateDropdownCurrent(e.NewValue), true);

            apiState.BindTo(api.State);
            apiState.BindValueChanged(onlineStateChanged, true);

            // changes to the active offline profile (its username, avatar etc.) are reflected by the local user.
            localUser.BindTo(api.LocalUser);
            localUser.BindValueChanged(_ => Scheduler.AddOnce(updateOfflineContent));

            if (offlineProfiles != null)
            {
                activeOfflineProfile.BindTo(offlineProfiles.ActiveProfile);
                activeOfflineProfile.BindValueChanged(_ => Scheduler.AddOnce(updateOfflineContent));
            }
        }

        private void updateOfflineContent()
        {
            if (apiState.Value == APIState.Offline)
                showOfflineContent();
        }

        private void onlineStateChanged(ValueChangedEvent<APIState> state) => Schedule(() =>
        {
            form = null;

            switch (state.NewValue)
            {
                case APIState.Offline:
                    showOfflineContent();
                    break;

                case APIState.RequiresSecondFactorAuth:
                    Child = form = new SecondFactorAuthForm();
                    break;

                case APIState.Failing:
                case APIState.Connecting:
                    LinkFlowContainer linkFlow;

                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_MARGINS },
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0f, SettingsSection.ITEM_SPACING),
                        Children = new Drawable[]
                        {
                            new LoadingSpinner
                            {
                                State = { Value = Visibility.Visible },
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                            },
                            linkFlow = new LinkFlowContainer
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                TextAnchor = Anchor.TopCentre,
                                AutoSizeAxes = Axes.Both,
                            },
                        },
                    };

                    if (!string.IsNullOrEmpty(api.UserFacingOutageMessage.Value))
                    {
                        linkFlow.AddText("Server outage in progress".ToUpperInvariant(), s =>
                        {
                            s.Font = OsuFont.Style.Caption2.With(weight: FontWeight.Bold);
                            s.Colour = Colour4.Orange;
                        });

                        linkFlow.AddParagraph(api.UserFacingOutageMessage.Value, s => s.Font = OsuFont.Style.Caption1);
                    }
                    else if (state.NewValue == APIState.Failing)
                    {
                        linkFlow.AddParagraph(state.NewValue == APIState.Failing ? ToolbarStrings.AttemptingToReconnect : ToolbarStrings.Connecting, s =>
                        {
                            s.Font = OsuFont.Style.Caption2.With(weight: FontWeight.Bold);
                            s.Colour = Colour4.Orange;
                        });
                    }
                    else
                    {
                        linkFlow.AddParagraph(ToolbarStrings.Connecting, s =>
                            s.Font = OsuFont.Style.Caption2.With(weight: FontWeight.Bold));
                    }

                    linkFlow.NewParagraph();
                    linkFlow.AddLink(LoginPanelStrings.SignOut, api.Logout, string.Empty, s => s.Font = OsuFont.Style.Caption2);
                    break;

                case APIState.Online:
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_MARGINS },
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0f, SettingsSection.ITEM_SPACING),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = LoginPanelStrings.SignedIn,
                                Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                            },
                            new UserRankPanel(api.LocalUser.Value)
                            {
                                RelativeSizeAxes = Axes.X,
                                Action = RequestHide
                            },
                            dropdown = new UserDropdown { RelativeSizeAxes = Axes.X },
                        },
                    };

                    updateDropdownCurrent(configUserStatus.Value);
                    dropdown.Current.BindValueChanged(action =>
                    {
                        switch (action.NewValue)
                        {
                            case UserAction.Online:
                                configUserStatus.Value = UserStatus.Online;
                                dropdown.StatusColour = colours.Green;
                                break;

                            case UserAction.DoNotDisturb:
                                configUserStatus.Value = UserStatus.DoNotDisturb;
                                dropdown.StatusColour = colours.Red;
                                break;

                            case UserAction.AppearOffline:
                                configUserStatus.Value = UserStatus.Offline;
                                dropdown.StatusColour = colours.Gray7;
                                break;

                            case UserAction.SignOut:
                                api.Logout();
                                break;
                        }
                    }, true);

                    break;
            }

            if (form != null)
                ScheduleAfterChildren(() => GetContainingFocusManager()?.ChangeFocus(form));
        });

        /// <summary>
        /// Shows the active offline profile like a logged in user, or the login form along with the selection of offline profiles.
        /// </summary>
        private void showOfflineContent()
        {
            form = null;

            var profile = offlineProfiles?.ActiveProfile.Value;

            if (profile == null)
            {
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        form = new LoginForm
                        {
                            RequestHide = RequestHide
                        },
                        new OfflineProfilePanel
                        {
                            RequestHide = RequestHide
                        },
                    }
                };

                return;
            }

            Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0f, SettingsSection.ITEM_SPACING),
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_MARGINS },
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0f, SettingsSection.ITEM_SPACING),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = OfflineProfileStrings.SignedInOffline,
                                Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                            },
                            new OfflineProfileRankPanel(profile)
                            {
                                RelativeSizeAxes = Axes.X,
                                Action = RequestHide
                            },
                            new OfflineProfileDropdown
                            {
                                Caption = OfflineProfileStrings.PlayAs,
                                HintText = OfflineProfileStrings.PlayAsDescription,
                            },
                        },
                    },
                    new SettingsButton
                    {
                        Text = OfflineProfileStrings.ManageProfiles,
                        Action = () =>
                        {
                            RequestHide?.Invoke();
                            settings?.ShowAtControl<OfflineProfileSettings>();
                        },
                    },
                    // signing out of the profile shows the login form again.
                    new DangerousSettingsButton
                    {
                        Text = LoginPanelStrings.SignOut,
                        Action = () => offlineProfiles?.SetActiveProfile(null),
                    },
                },
            };
        }

        private void updateDropdownCurrent(UserStatus? status)
        {
            if (dropdown == null)
                return;

            switch (status)
            {
                case UserStatus.Online:
                    dropdown.Current.Value = UserAction.Online;
                    break;

                case UserStatus.DoNotDisturb:
                    dropdown.Current.Value = UserAction.DoNotDisturb;
                    break;

                case UserStatus.Offline:
                    dropdown.Current.Value = UserAction.AppearOffline;
                    break;
            }
        }

        public override bool AcceptsFocus => true;

        protected override bool OnClick(ClickEvent e) => true;

        protected override void OnFocus(FocusEvent e)
        {
            if (form != null) GetContainingFocusManager()!.ChangeFocus(form);
            base.OnFocus(e);
        }
    }
}
