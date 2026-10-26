// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// Everything which can be done in the discussion, sent to the website within the <see cref="WebSession"/> like the website does it.
    /// </summary>
    public partial class DiscussionScreen
    {
        [Resolved(canBeNull: true)]
        private WebSession? webSession { get; set; }

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        private DiscussionPromptOverlay promptOverlay = null!;

        public void ShowPrompt(DiscussionPrompt prompt) => promptOverlay.Show(prompt);

        /// <summary>
        /// Asks for confirmation before doing something.
        /// </summary>
        public void Confirm(LocalisableString message, Action onConfirm)
        {
            if (dialogOverlay == null)
            {
                onConfirm();
                return;
            }

            dialogOverlay.Push(new ConfirmDialog(message, onConfirm));
        }

        /// <summary>
        /// Sends a request to the website, signing into it or verifying the session first if needed, and loads the discussion again once done.
        /// </summary>
        public void Send(HttpMethod method, string path, IEnumerable<KeyValuePair<string, string>>? form = null, Action? onSuccess = null)
        {
            var parameters = form?.ToList();
            Run(s => s.SendAsync(method, path, parameters), onSuccess);
        }

        /// <summary>
        /// Runs something within the website session, signing into it or verifying the session first if needed, and loads the discussion again once done.
        /// </summary>
        public void Run(Func<WebSession, Task> action, Action? onSuccess = null)
        {
            if (webSession == null)
            {
                showError(SlopDiscussionStrings.ActionUnavailable.ToString());
                return;
            }

            var session = webSession;

            if (!session.IsLoggedIn)
            {
                promptLogin(() => Run(action, onSuccess));
                return;
            }

            Task.Run(() => action(session)).ContinueWith(t => Schedule(() =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    onSuccess?.Invoke();
                    Session.RefreshNow();
                    return;
                }

                switch (t.Exception?.GetBaseException())
                {
                    case WebSessionVerificationException:
                        promptVerification(() => Run(action, onSuccess));
                        break;

                    case WebSessionLoginRequiredException:
                        promptLogin(() => Run(action, onSuccess));
                        break;

                    case Exception e:
                        showError(e.Message);
                        break;
                }
            }));
        }

        /// <summary>
        /// Like <see cref="Run"/>, for prompts which show the progress and the error themselves.
        /// </summary>
        public async Task RunInPrompt(Func<WebSession, Task> action)
        {
            if (webSession == null)
                throw new InvalidOperationException(SlopDiscussionStrings.ActionUnavailable.ToString());

            var session = webSession;

            try
            {
                await action(session).ConfigureAwait(false);
            }
            catch (WebSessionVerificationException)
            {
                Schedule(() => promptVerification(() => promptRetry(action)));
                throw new InvalidOperationException(SlopDiscussionStrings.VerificationRequired.ToString());
            }
            catch (WebSessionLoginRequiredException)
            {
                Schedule(() => promptLogin(() => promptRetry(action)));
                throw new InvalidOperationException(SlopDiscussionStrings.LoginRequired.ToString());
            }

            Schedule(() => Session.RefreshNow());
        }

        private void promptRetry(Func<WebSession, Task> action) => Run(action);

        private void showError(string message)
        {
            notifications?.Post(new SimpleErrorNotification { Text = SlopDiscussionStrings.ActionFailed(message) });
        }

        private void promptLogin(Action onSuccess)
        {
            var prompt = new DiscussionPrompt(SlopDiscussionStrings.LoginTitle, SlopDiscussionStrings.LoginDescription);
            var session = webSession!;

            var username = prompt.AddTextBox(SlopDiscussionStrings.LoginUsername, api.LocalUser.Value?.Username ?? string.Empty);
            var password = prompt.AddTextBox(SlopDiscussionStrings.LoginPassword, password: true);

            void login()
            {
                string name = username.Text.Trim();

                if (name.Length == 0 || password.Text.Length == 0)
                {
                    prompt.ShowError(SlopDiscussionStrings.PromptEmpty.ToString());
                    return;
                }

                prompt.Run(() => session.LoginOrSolveCaptchaAsync(name, password.Text), onSuccess);
            }

            password.OnCommit += (_, _) => login();
            prompt.AddButton(SlopDiscussionStrings.LoginButton, login);

            ShowPrompt(prompt);
        }

        private void promptVerification(Action onSuccess)
        {
            var prompt = new DiscussionPrompt(SlopDiscussionStrings.VerificationTitle, SlopDiscussionStrings.VerificationDescription);
            var session = webSession!;

            var code = prompt.AddTextBox(SlopDiscussionStrings.VerificationPlaceholder);

            void verify()
            {
                string key = code.Text.Replace(@" ", string.Empty);

                if (key.Length == 0)
                {
                    prompt.ShowError(SlopDiscussionStrings.PromptEmpty.ToString());
                    return;
                }

                prompt.Run(() => session.VerifyAsync(key), onSuccess);
            }

            code.OnCommit += (_, _) => verify();
            prompt.AddButton(SlopDiscussionStrings.VerificationResend, () => prompt.Run(() => session.ReissueVerificationAsync(), closeOnSuccess: false), false);
            prompt.AddButton(SlopDiscussionStrings.VerificationButton, verify);

            ShowPrompt(prompt);
        }

        #region Permissions

        /// <summary>
        /// Whether the signed in user is in one of the groups, e.g. "nat" or "bng".
        /// </summary>
        public bool IsCurrentUserInGroup(params string[] identifiers)
        {
            var user = api.LocalUser.Value;

            if (user?.Groups?.Any(g => identifiers.Contains(g.Identifier)) == true)
                return true;

            if (CurrentState.Value is DiscussionsState state && state.CurrentUserId is int id && state.Users.TryGetValue(id, out var discussionUser))
                return discussionUser.Groups?.Any(g => identifiers.Contains(g.Identifier)) == true;

            return false;
        }

        /// <summary>
        /// Moderators of the discussion, who can lock it and restore deleted posts.
        /// </summary>
        public bool IsCurrentUserModerator => IsCurrentUserInGroup(@"gmt", @"nat");

        /// <summary>
        /// Whether the user can nominate, like <c>nominationModes</c> on the website.
        /// </summary>
        public bool IsCurrentUserNominator => IsCurrentUserInGroup(@"bng", @"bng_limited", @"nat");

        public bool IsSignedIn => api.IsLoggedIn;

        /// <summary>
        /// Whether the website session is signed in, which is needed for everything done in the discussion.
        /// </summary>
        public bool IsSignedIntoWebsite => webSession?.IsLoggedIn == true;

        /// <summary>
        /// Signs into the website, after which the discussion is loaded again with what the user is allowed to do in it.
        /// </summary>
        public void SignIntoWebsite()
        {
            if (webSession == null)
            {
                showError(SlopDiscussionStrings.ActionUnavailable.ToString());
                return;
            }

            promptLogin(() => Session.RefreshNow());
        }

        #endregion

        #region Discussions

        /// <summary>
        /// Posts a new discussion, asking first if it disqualifies the beatmap set or resets its nominations.
        /// </summary>
        /// <param name="beatmapId">The beatmap of the discussion, or null for the general (all difficulties) page.</param>
        /// <param name="messageType">The type, e.g. "praise", "suggestion" or "problem".</param>
        /// <param name="timestamp">The timestamp in milliseconds, for the timeline.</param>
        /// <param name="message">The message.</param>
        /// <param name="onSuccess">Invoked once posted.</param>
        public void PostDiscussion(int? beatmapId, string messageType, int? timestamp, string message, Action? onSuccess = null)
        {
            var state = CurrentState.Value;

            if (state == null)
                return;

            var form = new List<KeyValuePair<string, string>>
            {
                p(@"beatmapset_id", state.BeatmapSet.Id),
                p(@"beatmap_discussion[message_type]", messageType),
                p(@"beatmap_discussion_post[message]", message),
            };

            if (beatmapId != null)
                form.Add(p(@"beatmap_discussion[beatmap_id]", beatmapId.Value));

            if (timestamp != null)
                form.Add(p(@"beatmap_discussion[timestamp]", timestamp.Value));

            void post() => Send(HttpMethod.Post, @"/beatmapsets/discussions/posts", form, onSuccess);

            if (messageType == @"problem")
                confirmProblem(state, post);
            else if (messageType == @"hype")
                Confirm(SlopDiscussionStrings.HypeConfirm(state.BeatmapSet.CurrentUserAttributes?.RemainingHype ?? 0), post);
            else
                post();
        }

        /// <summary>
        /// Like the website, asks before a problem disqualifies the beatmap set or resets its nominations.
        /// </summary>
        private void confirmProblem(DiscussionsState state, Action post)
        {
            if (state.BeatmapSet.Status == @"qualified" && IsCurrentUserInGroup(@"gmt", @"nat"))
                Confirm(SlopDiscussionStrings.DisqualifyConfirm, post);
            else if (state.BeatmapSet.Status == @"pending" && state.NominationsCurrent > 0 && IsCurrentUserInGroup(@"bng", @"bng_limited", @"gmt", @"nat"))
                Confirm(SlopDiscussionStrings.NominationResetConfirm, post);
            else
                post();
        }

        /// <summary>
        /// Submits a review, which is a document of paragraphs and embedded discussions.
        /// </summary>
        public void SubmitReview(string document, Action? onSuccess = null, bool hasProblems = false)
        {
            var state = CurrentState.Value;

            if (state == null)
                return;

            void post() => Send(HttpMethod.Post, $@"/beatmapsets/{state.BeatmapSet.Id}/discussion/review", new[] { p(@"document", document) }, onSuccess);

            if (hasProblems)
                confirmProblem(state, post);
            else
                post();
        }

        /// <summary>
        /// Replies to a discussion, resolving or reopening it at the same time if requested.
        /// </summary>
        public void Reply(BeatmapDiscussion discussion, string message, bool? resolved = null, Action? onSuccess = null)
        {
            var form = new List<KeyValuePair<string, string>>
            {
                p(@"beatmap_discussion_id", discussion.Id),
                p(@"beatmap_discussion_post[message]", message),
            };

            if (resolved != null)
                form.Add(p(@"beatmap_discussion[resolved]", resolved.Value ? @"1" : @"0"));

            Send(HttpMethod.Post, @"/beatmapsets/discussions/posts", form, onSuccess);
        }

        /// <summary>
        /// Votes for a discussion, or takes back the vote with a score of 0.
        /// </summary>
        public void Vote(BeatmapDiscussion discussion, int score)
            => Send(HttpMethod.Put, $@"/beatmapsets/discussions/{discussion.Id}/vote", new[] { p(@"beatmap_discussion_vote[score]", score) });

        public Task EditPost(WebSession session, BeatmapDiscussionPost post, string message)
            => session.SendAsync(HttpMethod.Put, $@"/beatmapsets/discussions/posts/{post.Id}", new[] { p(@"beatmap_discussion_post[message]", message) });

        public void DeletePost(BeatmapDiscussion discussion, BeatmapDiscussionPost post)
        {
            // deleting the first post deletes the discussion, like on the website.
            bool isStartingPost = discussion.StartingPost?.Id == post.Id;

            Confirm(isStartingPost ? SlopDiscussionStrings.DeleteDiscussionConfirm : SlopDiscussionStrings.DeletePostConfirm, () =>
                Send(HttpMethod.Delete, isStartingPost ? $@"/beatmapsets/discussions/{discussion.Id}" : $@"/beatmapsets/discussions/posts/{post.Id}"));
        }

        public void RestorePost(BeatmapDiscussion discussion, BeatmapDiscussionPost post)
        {
            bool isStartingPost = discussion.StartingPost?.Id == post.Id;

            Send(HttpMethod.Post, isStartingPost ? $@"/beatmapsets/discussions/{discussion.Id}/restore" : $@"/beatmapsets/discussions/posts/{post.Id}/restore");
        }

        public void AllowKudosu(BeatmapDiscussion discussion)
            => Confirm(SlopDiscussionStrings.AllowKudosuConfirm, () => Send(HttpMethod.Post, $@"/beatmapsets/discussions/{discussion.Id}/allow-kudosu"));

        public void DenyKudosu(BeatmapDiscussion discussion)
            => Confirm(SlopDiscussionStrings.DenyKudosuConfirm, () => Send(HttpMethod.Post, $@"/beatmapsets/discussions/{discussion.Id}/deny-kudosu"));

        #endregion

        #region Beatmap set

        public void ToggleWatch()
        {
            if (CurrentState.Value is not DiscussionsState state)
                return;

            bool watching = state.BeatmapSet.CurrentUserAttributes?.IsWatching == true;
            Send(watching ? HttpMethod.Delete : HttpMethod.Put, $@"/beatmapsets/watches/{state.BeatmapSet.Id}");
        }

        public void Nominate()
        {
            if (CurrentState.Value is not DiscussionsState state)
                return;

            var rulesets = state.RulesetsWithoutDeletedBeatmaps.ToList();
            var allowed = nominationModes(state);

            if (allowed.Count > 0)
                rulesets = rulesets.Where(allowed.Contains).ToList();

            Task nominate(WebSession session, IEnumerable<string> modes)
                => session.SendAsync(HttpMethod.Put, $@"/beatmapsets/{state.BeatmapSet.Id}/nominate", modes.Select(m => p(@"playmodes[]", m)));

            if (state.BeatmapSet.Nominations?.LegacyMode != false || rulesets.Count <= 1)
            {
                Confirm(SlopDiscussionStrings.NominateConfirm, () => Run(s => nominate(s, rulesets)));
                return;
            }

            ShowPrompt(new DiscussionChoicePrompt(SlopDiscussionStrings.NominateTitle, SlopDiscussionStrings.NominateDescription,
                rulesets.Select(r => (r, (LocalisableString)DiscussionDisplay.RulesetName(r), rulesets.Count == 1)),
                SlopDiscussionStrings.NominateButton, modes => RunInPrompt(s => nominate(s, modes))));
        }

        private static List<string> nominationModes(DiscussionsState state)
        {
            if (state.BeatmapSet.CurrentUserAttributes?.NominationModes is Newtonsoft.Json.Linq.JObject modes)
                return modes.Properties().Select(m => m.Name).ToList();

            return new List<string>();
        }

        public void Love()
        {
            if (CurrentState.Value is not DiscussionsState state)
                return;

            var beatmaps = state.SortedBeatmaps.Where(b => b.DeletedAt == null && !b.Convert).ToList();

            ShowPrompt(new DiscussionChoicePrompt(SlopDiscussionStrings.LoveTitle, SlopDiscussionStrings.LoveDescription,
                beatmaps.Select(b => (b.Id.ToString(CultureInfo.InvariantCulture), (LocalisableString)$@"[{DiscussionDisplay.RulesetName(b.Mode)}] {b.Version}", true)),
                SlopDiscussionStrings.LoveButton,
                ids => RunInPrompt(s => s.SendAsync(HttpMethod.Put, $@"/beatmapsets/{state.BeatmapSet.Id}/love", ids.Select(id => p(@"beatmap_ids[]", id))))));
        }

        public void RemoveFromLoved()
        {
            if (CurrentState.Value is not DiscussionsState state)
                return;

            ShowPrompt(new DiscussionTextPrompt(SlopDiscussionStrings.RemoveFromLovedTitle, SlopDiscussionStrings.RemoveFromLovedDescription, SlopDiscussionStrings.ReasonPlaceholder,
                SlopDiscussionStrings.RemoveFromLovedButton,
                reason => RunInPrompt(s => s.SendAsync(HttpMethod.Delete, $@"/beatmapsets/{state.BeatmapSet.Id}/remove-from-loved", new[] { p(@"reason", reason) }))));
        }

        public void LockDiscussion()
        {
            if (CurrentState.Value is not DiscussionsState state)
                return;

            ShowPrompt(new DiscussionTextPrompt(SlopDiscussionStrings.LockTitle, SlopDiscussionStrings.LockDescription, SlopDiscussionStrings.ReasonPlaceholder,
                SlopDiscussionStrings.LockButton,
                reason => RunInPrompt(s => s.SendAsync(HttpMethod.Post, $@"/beatmapsets/{state.BeatmapSet.Id}/discussion-lock", new[] { p(@"reason", reason) }))));
        }

        public void UnlockDiscussion()
        {
            if (CurrentState.Value is not DiscussionsState state)
                return;

            Confirm(SlopDiscussionStrings.UnlockConfirm, () => Send(HttpMethod.Post, $@"/beatmapsets/{state.BeatmapSet.Id}/discussion-unlock"));
        }

        public void DeleteBeatmapSet()
        {
            if (CurrentState.Value is not DiscussionsState state)
                return;

            Confirm(SlopDiscussionStrings.DeleteBeatmapSetConfirm, () => Send(HttpMethod.Delete, $@"/beatmapsets/{state.BeatmapSet.Id}"));
        }

        /// <summary>
        /// Changes the mappers of a difficulty, entered as usernames or IDs separated by commas.
        /// </summary>
        public void ChangeOwners(DiscussionBeatmap beatmap)
        {
            if (CurrentState.Value is not DiscussionsState state)
                return;

            string current = string.Join(@", ", beatmap.Owners?.Select(o => o.Username) ?? Enumerable.Empty<string>());

            ShowPrompt(new DiscussionTextPrompt(SlopDiscussionStrings.ChangeOwnersTitle(beatmap.Version), SlopDiscussionStrings.ChangeOwnersDescription, SlopDiscussionStrings.ChangeOwnersPlaceholder,
                SlopDiscussionStrings.ChangeOwnersButton, async text =>
                {
                    var ids = new List<long>();

                    foreach (string name in text.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0))
                        ids.Add(await lookupUserId(state, name).ConfigureAwait(false));

                    await RunInPrompt(s => s.SendAsync(HttpMethod.Post, $@"/beatmaps/{beatmap.Id}/update-owner", ids.Select(id => p(@"user_ids[]", id)))).ConfigureAwait(false);
                }, current));
        }

        private async Task<long> lookupUserId(DiscussionsState state, string name)
        {
            var known = state.Users.Values.FirstOrDefault(u => string.Equals(u.Username, name, StringComparison.OrdinalIgnoreCase));

            if (known != null)
                return known.Id;

            var request = long.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out long id) ? new GetUserRequest(id) : new GetUserRequest(name);
            var completion = new TaskCompletionSource<APIUser>();

            request.Success += user => completion.TrySetResult(user);
            request.Failure += _ => completion.TrySetException(new InvalidOperationException(SlopDiscussionStrings.UserNotFound(name).ToString()));

            api.Queue(request);

            var found = await completion.Task.ConfigureAwait(false);
            return found.OnlineID;
        }

        #endregion

        private static KeyValuePair<string, string> p(string key, string value) => new KeyValuePair<string, string>(key, value);

        private static KeyValuePair<string, string> p(string key, long value) => new KeyValuePair<string, string>(key, value.ToString(CultureInfo.InvariantCulture));
    }
}
