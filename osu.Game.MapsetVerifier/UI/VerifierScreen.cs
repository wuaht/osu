// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Input.Bindings;
using osu.Game.MapsetVerifier.UI.Checks;
using osu.Game.MapsetVerifier.UI.Documentation;
using osu.Game.MapsetVerifier.UI.Overview;
using osu.Game.MapsetVerifier.UI.Snapshots;
using osu.Game.Screens.Edit;
using osuTK.Input;

namespace osu.Game.MapsetVerifier.UI
{
    public enum VerifierTabType
    {
        Checks,
        Documentation,
        Snapshots,
        Overview,
    }

    /// <summary>
    /// The verify screen of the editor, which checks the beatmap set with Mapset Verifier.
    /// </summary>
    [Cached]
    public partial class VerifierScreen : EditorScreen, IKeyBindingHandler<GlobalAction>
    {
        public const float SPACING = 10;

        public readonly Bindable<VerifierTabType> CurrentTab = new Bindable<VerifierTabType>();

        /// <summary>
        /// Invoked on the update thread whenever anything in the <see cref="Session"/> changed.
        /// </summary>
        public event Action? SessionChanged;

        public VerifierSession Session { get; private set; } = null!;

        [Resolved]
        private Editor? editor { get; set; }

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private readonly Bindable<bool> includeDelta = new Bindable<bool>();
        private readonly Bindable<bool> createSnapshot = new Bindable<bool>();
        private readonly Bindable<bool> includeTimings = new Bindable<bool>();
        private readonly Bindable<bool> recheckOnSave = new Bindable<bool>();

        private readonly Dictionary<VerifierTabType, VerifierTab> tabs = new Dictionary<VerifierTabType, VerifierTab>();

        private int lastVersion = -1;

        /// <summary>
        /// The tab which was shown last, which is shown again when the verify screen is opened again (e.g. after switching difficulties).
        /// </summary>
        private static VerifierTabType lastTab;

        public VerifierScreen()
            : base(EditorScreenMode.Verify)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Session = VerifierSession.For(EditorBeatmap.BeatmapInfo.BeatmapSet!, storage);

            // a difficulty which was switched to elsewhere (e.g. using the menu) is the one which is shown.
            if (Session.SelectedCategory != VerifierSession.GENERAL)
                Session.SelectedCategory = EditorBeatmap.BeatmapInfo.DifficultyName;

            config.BindWith(OsuSetting.SlopMapsetVerifierShowCheckRunDelta, includeDelta);
            config.BindWith(OsuSetting.SlopMapsetVerifierAutoSnapshot, createSnapshot);
            config.BindWith(OsuSetting.SlopMapsetVerifierShowCheckSpeedStats, includeTimings);
            config.BindWith(OsuSetting.SlopMapsetVerifierRecheckOnSave, recheckOnSave);

            tabs[VerifierTabType.Checks] = new ChecksTab();
            tabs[VerifierTabType.Documentation] = new DocumentationTab();
            tabs[VerifierTabType.Snapshots] = new SnapshotsTab();
            tabs[VerifierTabType.Overview] = new OverviewTab();

            foreach (var tab in tabs.Values)
            {
                tab.RelativeSizeAxes = Axes.Both;
                tab.Alpha = 0;
            }

            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(SPACING),
                Child = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.Absolute, SPACING),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[] { new VerifierHeader() },
                        new[] { Empty() },
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = tabs.Values.ToArray(),
                            }
                        },
                    }
                }
            };

            CurrentTab.Value = lastTab;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            CurrentTab.BindValueChanged(t =>
            {
                lastTab = t.NewValue;

                foreach (var (type, tab) in tabs)
                {
                    if (type == t.NewValue)
                    {
                        tab.FadeIn(200, Easing.OutQuint);
                        tab.OnShown();
                    }
                    else
                        tab.FadeOut(200, Easing.OutQuint);
                }
            }, true);

            if (editor != null)
                editor.Saved += onSaved;
        }

        protected override void Update()
        {
            base.Update();

            int version = Session.Version;

            if (version == lastVersion)
                return;

            lastVersion = version;
            SessionChanged?.Invoke();
        }

        protected override void PopIn()
        {
            base.PopIn();

            // the beatmap set may have changed while another screen was shown.
            Check();
        }

        private void onSaved()
        {
            if (recheckOnSave.Value && State.Value == Visibility.Visible)
                Schedule(() => Check());
        }

        /// <summary>
        /// Checks the beatmap set, if it changed since the last check run (or always, if <paramref name="force"/> is set).
        /// </summary>
        public void Check(bool force = false)
        {
            var options = new VerifierCheckOptions(includeDelta.Value, createSnapshot.Value, includeTimings.Value);
            Session.Check(SetMaterializer.Capture(EditorBeatmap, editor?.HasUnsavedChanges ?? false), options, force);
        }

        /// <summary>
        /// The difficulty of the beatmap set which a category of issues refers to, or null for <see cref="VerifierSession.GENERAL"/>.
        /// </summary>
        public BeatmapInfo? FindDifficulty(string category)
        {
            if (category == VerifierSession.GENERAL)
                return null;

            var difficulties = EditorBeatmap.BeatmapInfo.BeatmapSet?.Beatmaps;

            if (difficulties == null)
                return null;

            if (EditorBeatmap.BeatmapInfo.DifficultyName == category)
                return EditorBeatmap.BeatmapInfo;

            return difficulties.FirstOrDefault(b => b.DifficultyName == category);
        }

        /// <summary>
        /// Selects a category of issues, and switches the editor to its difficulty.
        /// </summary>
        public void SelectCategory(string category)
        {
            if (Session.SelectedCategory == category)
                return;

            Session.SelectedCategory = category;
            SessionChanged?.Invoke();

            var difficulty = FindDifficulty(category);

            if (difficulty != null && !difficulty.Equals(EditorBeatmap.BeatmapInfo))
            {
                // switching recreates the editor, so the selection is shown once the transition finished instead of in the middle of it.
                editor?.SwitchToDifficulty(difficulty);
            }
        }

        /// <summary>
        /// Selects the previous or next category of issues.
        /// </summary>
        public void SelectAdjacentCategory(int direction)
        {
            var categories = Session.Results?.Categories;

            if (categories == null || categories.Count == 0)
                return;

            int index = categories.FindIndex(c => c.Name == Session.SelectedCategory);
            int next = Math.Clamp(index + direction, 0, categories.Count - 1);

            SelectCategory(categories[next].Name);
        }

        /// <summary>
        /// Shows the object which an editor timestamp refers to in the compose screen.
        /// </summary>
        /// <param name="timestamp">The timestamp.</param>
        /// <param name="category">The category of the issue containing the timestamp, which decides the difficulty to show it in.</param>
        public void ShowTimestamp(string timestamp, string category)
        {
            var difficulty = FindDifficulty(category);

            if (category != VerifierSession.GENERAL)
                Session.SelectedCategory = category;

            editor?.ShowTimestampInCompose(timestamp, difficulty);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            if (e.Action == GlobalAction.Back && tabs[CurrentTab.Value].OnBack())
                return true;

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.AltPressed || e.ShiftPressed || e.Repeat)
                return base.OnKeyDown(e);

            // plain F5 and brackets are already bound by the editor (test gameplay and slider velocity), and page up/down scroll the hovered list.
            if (e.ControlPressed)
            {
                if (e.Key != Key.F5)
                    return base.OnKeyDown(e);

                Check(true);
                return true;
            }

            switch (e.Key)
            {
                case Key.Left when CurrentTab.Value == VerifierTabType.Checks || CurrentTab.Value == VerifierTabType.Snapshots:
                    SelectAdjacentCategory(-1);
                    return true;

                case Key.Right when CurrentTab.Value == VerifierTabType.Checks || CurrentTab.Value == VerifierTabType.Snapshots:
                    SelectAdjacentCategory(1);
                    return true;
            }

            return base.OnKeyDown(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (editor != null)
                editor.Saved -= onSaved;
        }
    }

    /// <summary>
    /// A tab of the verify screen.
    /// </summary>
    public abstract partial class VerifierTab : CompositeDrawable
    {
        [Resolved]
        protected VerifierScreen Screen { get; private set; } = null!;

        protected VerifierSession Session => Screen.Session;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Screen.SessionChanged += OnSessionChanged;
            OnSessionChanged();
        }

        /// <summary>
        /// Invoked whenever anything in the session changed.
        /// </summary>
        protected abstract void OnSessionChanged();

        /// <summary>
        /// Invoked when the tab is shown.
        /// </summary>
        public virtual void OnShown()
        {
        }

        /// <summary>
        /// Handles going back (e.g. closing something which is open).
        /// </summary>
        /// <returns>Whether going back was handled.</returns>
        public virtual bool OnBack() => false;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (Screen.IsNotNull())
                Screen.SessionChanged -= OnSessionChanged;
        }
    }
}
