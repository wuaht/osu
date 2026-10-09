// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.OfflineProfiles;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneOfflineProfileLocalUser : OsuTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private LocalUserState state = null!;

        private readonly OfflineProfileUser profileUser = new OfflineProfile { UserID = OfflineProfile.FIRST_USER_ID, Username = "offline" }.CreateUser();

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create state", () => Child = state = new LocalUserState(API, config));
        }

        [Test]
        public void TestOfflineProfileReplacesGuest()
        {
            AddAssert("guest", () => state.User.Value.Id, () => Is.EqualTo(APIUser.SYSTEM_USER_ID));

            AddStep("activate profile", () => state.SetOfflineProfileUser(profileUser));
            AddAssert("profile is local user", () => state.User.Value, () => Is.SameAs(profileUser));
            AddAssert("logged out user", () => state.HasLoggedOutUser);

            AddStep("deactivate profile", () => state.SetOfflineProfileUser(null));
            AddAssert("guest", () => state.User.Value.Id, () => Is.EqualTo(APIUser.SYSTEM_USER_ID));
        }

        [Test]
        public void TestChangesAreNotified()
        {
            IBindable<APIUser> boundUser = null!;
            APIUser? notifiedUser = null;

            // e.g. the toolbar displays the local user from a bound copy.
            AddStep("bind to local user", () =>
            {
                boundUser = state.User.GetBoundCopy();
                boundUser.BindValueChanged(u => notifiedUser = u.NewValue);
            });

            AddStep("activate profile", () => state.SetOfflineProfileUser(profileUser));
            AddAssert("profile notified", () => notifiedUser, () => Is.SameAs(profileUser));

            var renamedUser = new OfflineProfile { UserID = OfflineProfile.FIRST_USER_ID, Username = "renamed" }.CreateUser();

            AddStep("rename profile", () => state.SetOfflineProfileUser(renamedUser));
            AddAssert("renamed profile notified", () => notifiedUser, () => Is.SameAs(renamedUser));

            AddStep("deactivate profile", () => state.SetOfflineProfileUser(null));
            AddAssert("guest notified", () => notifiedUser?.Id, () => Is.EqualTo(APIUser.SYSTEM_USER_ID));
        }

        [Test]
        public void TestLoginReplacesProfile()
        {
            AddStep("activate profile", () => state.SetOfflineProfileUser(profileUser));

            // attempting to log in replaces the profile, as profiles are only used while not logged in.
            AddStep("attempt login", () => state.SetPlaceholderLocalUser("online"));
            AddAssert("placeholder is local user", () => state.User.Value.Username, () => Is.EqualTo("online"));
            AddAssert("not logged out", () => !state.HasLoggedOutUser);

            // changing the profile while logged in doesn't affect the local user.
            AddStep("change profile", () => state.SetOfflineProfileUser(null));
            AddAssert("placeholder still local user", () => state.User.Value.Username, () => Is.EqualTo("online"));

            AddStep("activate profile", () => state.SetOfflineProfileUser(profileUser));
            AddStep("log out", () => state.ClearLocalUser());
            AddUntilStep("profile is local user", () => state.User.Value, () => Is.SameAs(profileUser));
        }
    }
}
