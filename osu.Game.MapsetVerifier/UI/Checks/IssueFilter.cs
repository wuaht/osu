// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MapsetVerifier.Framework.Objects;
using osu.Framework.Bindables;
using osu.Game.Configuration;

namespace osu.Game.MapsetVerifier.UI.Checks
{
    /// <summary>
    /// Which issues are shown in the checks tab.
    /// </summary>
    public class IssueFilter
    {
        /// <summary>
        /// Whether issues of the levels other than <see cref="Issue.Level.Minor"/> are shown.
        /// These aren't settings, but are kept while the game runs.
        /// </summary>
        private static readonly Dictionary<Issue.Level, BindableBool> shown_levels = new Dictionary<Issue.Level, BindableBool>
        {
            [Issue.Level.Problem] = new BindableBool(true),
            [Issue.Level.Warning] = new BindableBool(true),
            [Issue.Level.Info] = new BindableBool(true),
            [Issue.Level.Error] = new BindableBool(true),
        };

        public readonly Bindable<bool> ShowMinor = new Bindable<bool>();

        private readonly Bindable<string> hiddenMinorChecks = new Bindable<string>();

        private readonly Dictionary<Issue.Level, BindableBool> levels = new Dictionary<Issue.Level, BindableBool>();

        /// <summary>
        /// Invoked whenever anything changed which decides which issues are shown.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// The checks whose negligible issues are always hidden.
        /// </summary>
        public IReadOnlySet<int> HiddenMinorChecks { get; private set; } = new HashSet<int>();

        /// <summary>
        /// The issue which is shown in the sidebar, if any.
        /// </summary>
        public readonly Bindable<(VerifierIssue Issue, string Category)?> SelectedIssue = new Bindable<(VerifierIssue Issue, string Category)?>();

        public IssueFilter(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopMapsetVerifierShowMinor, ShowMinor);
            config.BindWith(OsuSetting.SlopMapsetVerifierHiddenMinorChecks, hiddenMinorChecks);

            foreach (var (level, shown) in shown_levels)
            {
                // bound copies, such that the filter can be disposed of without leaking subscriptions to the static bindables.
                var copy = (BindableBool)shown.GetBoundCopy();
                copy.BindValueChanged(_ => Changed?.Invoke());
                levels[level] = copy;
            }

            ShowMinor.BindValueChanged(_ => Changed?.Invoke());
            hiddenMinorChecks.BindValueChanged(h =>
            {
                HiddenMinorChecks = parse(h.NewValue);
                Changed?.Invoke();
            }, true);
        }

        /// <summary>
        /// The bindable deciding whether issues of a level are shown.
        /// </summary>
        public Bindable<bool> GetLevel(Issue.Level level) => level == Issue.Level.Minor ? ShowMinor : levels[level];

        /// <summary>
        /// Whether an issue is shown.
        /// </summary>
        public bool IsShown(VerifierIssue issue)
        {
            var level = VerifierDisplay.GetDisplayLevel(issue.Level);

            if (level == Issue.Level.Minor)
                return ShowMinor.Value && !HiddenMinorChecks.Contains(issue.CheckId);

            return !levels.TryGetValue(level, out var shown) || shown.Value;
        }

        /// <summary>
        /// Hides or shows the negligible issues of a check.
        /// </summary>
        public void SetMinorHidden(int checkId, bool hidden)
        {
            var ids = new HashSet<int>(HiddenMinorChecks);

            if (hidden)
                ids.Add(checkId);
            else
                ids.Remove(checkId);

            hiddenMinorChecks.Value = string.Join(',', ids.OrderBy(i => i));
        }

        private static HashSet<int> parse(string value)
        {
            var ids = new HashSet<int>();

            foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out int id))
                    ids.Add(id);
            }

            return ids;
        }

        public void UnbindAll()
        {
            ShowMinor.UnbindAll();
            hiddenMinorChecks.UnbindAll();

            foreach (var level in levels.Values)
                level.UnbindAll();
        }
    }
}
