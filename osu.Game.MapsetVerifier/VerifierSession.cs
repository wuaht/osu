// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MapsetVerifier.Framework;
using MapsetVerifier.Framework.Objects;
using MapsetVerifier.Server.Model;
using MapsetVerifier.Server.Model.AudioAnalysis;
using MapsetVerifier.Server.Model.BeatmapAnalysis;
using MapsetVerifier.Server.Model.MetadataAnalysis;
using MapsetVerifier.Server.Model.VideoAnalysis;
using MapsetVerifier.Server.Service;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using MvBeatmap = MapsetVerifier.Parser.Objects.Beatmap;
using MvBeatmapSet = MapsetVerifier.Parser.Objects.BeatmapSet;

namespace osu.Game.MapsetVerifier
{
    public enum VerifierRunState
    {
        NotRun,
        Running,
        Ready,
        Failed,
    }

    /// <summary>
    /// The verification of a beatmap set. The editor is recreated when switching difficulties, so sessions outlive the verify screen,
    /// which lets switching difficulties show the results immediately instead of checking the beatmap set again.
    /// </summary>
    /// <remarks>
    /// All engine work happens on background threads, one task at a time. The screen polls <see cref="Version"/> to find out about changes.
    /// </remarks>
    public class VerifierSession
    {
        /// <summary>
        /// The name of the category of issues which concern the whole beatmap set.
        /// </summary>
        public const string GENERAL = @"General";

        private static readonly Dictionary<string, VerifierSession> sessions = new Dictionary<string, VerifierSession>();

        /// <summary>
        /// The session of a beatmap set, which is created on first use.
        /// </summary>
        public static VerifierSession For(BeatmapSetInfo set, Storage storage)
        {
            string key = set.OnlineID > 0 ? set.OnlineID.ToString() : set.ID.ToString();

            lock (sessions)
            {
                if (!sessions.TryGetValue(key, out var session))
                    sessions[key] = session = new VerifierSession(storage, storage.GetFullPath(Path.Combine(@"mapset-verifier", @"sets", key)));

                return session;
            }
        }

        /// <summary>
        /// The song folder which the beatmap set is written to for Mapset Verifier.
        /// </summary>
        public readonly string Folder;

        private readonly Storage storage;

        private readonly object queueLock = new object();
        private Task queue = Task.CompletedTask;

        private volatile int version;

        /// <summary>
        /// Increased whenever anything in the session changes.
        /// </summary>
        public int Version => version;

        public VerifierRunState State { get; private set; } = VerifierRunState.NotRun;

        /// <summary>
        /// The progress of the current check run.
        /// </summary>
        public CheckProgress? Progress { get; private set; }

        public string? Error { get; private set; }

        /// <summary>
        /// The results of the latest check run.
        /// </summary>
        public VerifierResults? Results { get; private set; }

        /// <summary>
        /// The category (<see cref="GENERAL"/> or the name of a difficulty) which is selected in the checks and snapshots.
        /// </summary>
        public string SelectedCategory = GENERAL;

        /// <summary>
        /// The difficulty levels which difficulties are interpreted as, instead of the detected ones.
        /// </summary>
        public readonly Dictionary<string, MvBeatmap.Difficulty> LevelOverrides = new Dictionary<string, MvBeatmap.Difficulty>();

        public ApiSnapshotResult? Snapshots { get; private set; }

        public VerifierOverview? Overview { get; private set; }

        private string? lastHash;
        private bool snapshotsRequested;
        private bool overviewRequested;
        private bool advancedAudioRequested;

        private VerifierSession(Storage storage, string folder)
        {
            this.storage = storage;
            Folder = folder;
        }

        /// <summary>
        /// Checks the beatmap set, if it changed since the last check run (or always, if <paramref name="force"/> is set).
        /// </summary>
        /// <returns>Whether a check run was started.</returns>
        public bool Check(SetMaterializer files, VerifierCheckOptions options, bool force = false)
        {
            if (!force && files.Hash == lastHash && State != VerifierRunState.Failed)
                return false;

            lastHash = files.Hash;

            State = VerifierRunState.Running;
            Progress = null;
            Error = null;
            changed();

            enqueue(() =>
            {
                try
                {
                    files.WriteTo(storage, Folder);

                    var progress = new SynchronousProgress(p =>
                    {
                        Progress = p;
                        changed();
                    });

                    var result = BeatmapService.RunBeatmapSetChecks(Folder, progress, options.IncludeDelta, options.CreateSnapshot, options.IncludeTimings);
                    var results = VerifierResults.From(result);

                    foreach (var (difficulty, level) in LevelOverrides.ToArray())
                    {
                        if (results.Categories.All(c => c.Name != difficulty))
                            continue;

                        applyOverride(results, difficulty, level);
                    }

                    Results = results;
                    State = VerifierRunState.Ready;

                    // everything else depends on the files, so is outdated now.
                    Snapshots = null;
                    Overview = null;
                }
                catch (Exception e)
                {
                    Logger.Error(e, @"Mapset Verifier failed to check the beatmap set.");

                    Error = e.Message;
                    State = VerifierRunState.Failed;
                }

                changed();

                if (snapshotsRequested)
                    loadSnapshots();
                if (overviewRequested)
                    loadOverview();
            });

            return true;
        }

        /// <summary>
        /// Interprets a difficulty as a different difficulty level, or as the detected one again if <paramref name="level"/> is null.
        /// </summary>
        public void SetLevelOverride(string difficulty, MvBeatmap.Difficulty? level)
        {
            var results = Results;

            if (results == null)
                return;

            var category = results.Categories.FirstOrDefault(c => c.Name == difficulty);

            if (category == null)
                return;

            if (level == null || level == category.DetectedLevel)
                LevelOverrides.Remove(difficulty);
            else
                LevelOverrides[difficulty] = level.Value;

            var effectiveLevel = level ?? category.DetectedLevel;

            if (effectiveLevel == null)
                return;

            category.IsReinterpreting = true;
            changed();

            enqueue(() =>
            {
                try
                {
                    applyOverride(results, difficulty, effectiveLevel.Value);
                }
                catch (Exception e)
                {
                    Logger.Error(e, @"Mapset Verifier failed to check the difficulty with a different difficulty level.");
                }

                category.IsReinterpreting = false;
                changed();
            });
        }

        private void applyOverride(VerifierResults results, string difficulty, MvBeatmap.Difficulty level)
        {
            var overridden = BeatmapService.RunDifficultyCheckWithOverride(Folder, difficulty, level);

            if (overridden == null)
                return;

            results.ReplaceIssues(difficulty, level, overridden.Value);
        }

        /// <summary>
        /// Clears the check run history, such that the next check run doesn't show any changes.
        /// </summary>
        public void ClearHistory()
        {
            enqueue(() =>
            {
                try
                {
                    CheckRunHistoryService.ClearHistory(new MvBeatmapSet(Folder));
                }
                catch (Exception e)
                {
                    Logger.Error(e, @"Mapset Verifier failed to clear the check run history.");
                }

                if (Results != null)
                    Results.Delta = null;

                changed();
            });
        }

        /// <summary>
        /// Loads the snapshots of the beatmap set, if they aren't loaded yet.
        /// </summary>
        public void RequestSnapshots()
        {
            if (snapshotsRequested)
                return;

            snapshotsRequested = true;

            if (lastHash != null)
                loadSnapshots();
        }

        /// <summary>
        /// Analyses the beatmap set for the overview, if it isn't analysed yet.
        /// </summary>
        public void RequestOverview()
        {
            if (overviewRequested)
                return;

            overviewRequested = true;

            if (lastHash != null)
                loadOverview();
        }

        /// <summary>
        /// Runs the spectral and frequency analysis of the audio, which takes a while.
        /// </summary>
        public void RequestAdvancedAudio()
        {
            if (advancedAudioRequested)
                return;

            advancedAudioRequested = true;

            var overview = Overview;

            if (overview != null)
                loadAdvancedAudio(overview);
        }

        private void loadSnapshots() => enqueue(() =>
        {
            if (Snapshots != null)
                return;

            try
            {
                Snapshots = materialise(SnapshotService.GetSnapshots(Folder));
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Mapset Verifier failed to load the snapshots.");
                Snapshots = new ApiSnapshotResult(Array.Empty<ApiSnapshotDifficulty>(), null, Array.Empty<ApiSnapshotHistory>(), e.Message);
            }

            changed();
        });

        /// <summary>
        /// Evaluates all lazily evaluated parts of the snapshots, so that they can be read on the update thread.
        /// </summary>
        private static ApiSnapshotResult materialise(ApiSnapshotResult result)
        {
            return new ApiSnapshotResult(result.Difficulties.ToArray(), result.General == null ? null : history(result.General.Value),
                result.BeatmapHistories.Select(history).ToArray(), result.ErrorMessage);

            static ApiSnapshotHistory history(ApiSnapshotHistory h) => new ApiSnapshotHistory(h.DifficultyName, h.Commits.Select(c => new ApiSnapshotCommit(
                c.Date, c.Id, c.TotalChanges, c.Additions, c.Removals, c.Modifications,
                c.Sections.Select(s => new ApiSnapshotSection(s.Name, s.AggregatedDiffType, s.Additions, s.Removals, s.Modifications,
                    s.Diffs.Select(d => new ApiSnapshotDiff(d.Message, d.DiffType, d.OldValue, d.NewValue, d.Details.ToArray())).ToArray())).ToArray(),
                c.HasSnapshot)).ToArray());
        }

        private void loadOverview() => enqueue(() =>
        {
            if (Overview != null)
                return;

            var overview = Overview = new VerifierOverview();
            changed();

            overview.Metadata = analyse(() => MetadataAnalysisService.Analyze(Folder));
            overview.Beatmap = analyse(() => BeatmapAnalysisService.Analyze(Folder));
            overview.Objects = analyse(() => BeatmapAnalysisService.AnalyzeObjects(Folder));
            overview.Audio = analyse(() => (AudioAnalysisResult?)AudioAnalysisService.AnalyzeAudio(Folder));
            overview.Video = analyse(() => VideoAnalysisService.AnalyzeVideos(Folder));
            overview.Difficulty = analyse(() => BeatmapAnalysisService.AnalyzeDifficulty(Folder));

            if (advancedAudioRequested)
                loadAdvancedAudio(overview);

            T? analyse<T>(Func<T?> func)
            {
                T? result;

                try
                {
                    result = func();
                }
                catch (Exception e)
                {
                    Logger.Error(e, @"Mapset Verifier failed to analyse the beatmap set.");
                    overview.Errors.Add(e.Message);
                    result = default;
                }

                overview.CompletedAnalyses++;
                changed();
                return result;
            }
        });

        private void loadAdvancedAudio(VerifierOverview overview) => enqueue(() =>
        {
            if (overview.Spectral != null)
                return;

            try
            {
                overview.Frequency = AudioAnalysisService.GetFrequencyAnalysis(Folder);
                overview.Spectral = AudioAnalysisService.GetSpectralAnalysis(Folder, timeResolutionMs: 100);
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Mapset Verifier failed to analyse the frequencies of the audio.");
                overview.AdvancedAudioError = e.Message;
            }

            changed();
        });

        private void enqueue(Action action)
        {
            lock (queueLock)
                queue = queue.ContinueWith(_ => VerifierEngine.Run(() =>
                {
                    action();
                    return true;
                }), TaskScheduler.Default);
        }

        private void changed() => Interlocked.Increment(ref version);

        /// <summary>
        /// Reports progress on the thread doing the work, unlike <see cref="Progress{T}"/> which posts to the thread pool and can report out of order.
        /// </summary>
        private class SynchronousProgress : IProgress<CheckProgress>
        {
            private readonly Action<CheckProgress> report;

            public SynchronousProgress(Action<CheckProgress> report)
            {
                this.report = report;
            }

            public void Report(CheckProgress value) => report(value);
        }
    }

    public readonly record struct VerifierCheckOptions(bool IncludeDelta, bool CreateSnapshot, bool IncludeTimings);

    /// <summary>
    /// The analyses shown in the overview, which are filled in one after another.
    /// </summary>
    public class VerifierOverview
    {
        public MetadataAnalysisResult? Metadata;
        public BeatmapAnalysisResult? Beatmap;
        public ObjectsOverviewResult? Objects;
        public DifficultyOverviewResult? Difficulty;
        public AudioAnalysisResult? Audio;
        public VideoAnalysisResult? Video;

        public FrequencyAnalysisResult? Frequency;
        public SpectralAnalysisResult? Spectral;
        public string? AdvancedAudioError;

        /// <summary>
        /// How many of the analyses have finished, in the order in which they are declared above.
        /// </summary>
        public int CompletedAnalyses;

        public readonly List<string> Errors = new List<string>();
    }

    /// <summary>
    /// The results of a check run, with all lazily evaluated parts evaluated.
    /// </summary>
    public class VerifierResults
    {
        public readonly List<VerifierCategory> Categories = new List<VerifierCategory>();

        public readonly Dictionary<int, string> CheckNames = new Dictionary<int, string>();

        public ApiCheckRunDelta? Delta;

        public CheckTimingReport? Timings;

        public readonly DateTime RunAt = DateTime.Now;

        public VerifierCategory? Get(string name) => Categories.FirstOrDefault(c => c.Name == name);

        public static VerifierResults From(ApiBeatmapSetCheckResult result)
        {
            var results = new VerifierResults
            {
                Delta = result.CheckRunDelta == null ? null : materialise(result.CheckRunDelta.Value),
                Timings = result.CheckTimings,
            };

            foreach (var (id, check) in result.Checks)
                results.CheckNames[id] = check.Name;

            results.Categories.Add(results.createCategory(result.General, true));

            foreach (var difficulty in result.Difficulties)
                results.Categories.Add(results.createCategory(difficulty, false));

            return results;
        }

        public void ReplaceIssues(string difficulty, MvBeatmap.Difficulty level, ApiCategoryOverrideCheckResult result)
        {
            var category = Get(difficulty);

            if (category == null)
                return;

            foreach (var (id, check) in result.Checks)
                CheckNames[id] = check.Name;

            var replacement = createCategory(result.CategoryResult, false);

            lock (category)
            {
                category.Level = level;
                category.Issues = replacement.Issues;
            }
        }

        private VerifierCategory createCategory(ApiCategoryCheckResult category, bool isGeneral) => new VerifierCategory
        {
            Name = category.Category,
            IsGeneral = isGeneral,
            BeatmapId = category.BeatmapId,
            Mode = category.Mode,
            DetectedLevel = category.DifficultyLevel,
            Level = category.DifficultyLevel,
            StarRating = category.StarRating,
            Issues = category.CheckResults.Select(r => new VerifierIssue(r.Id, CheckNames.GetValueOrDefault(r.Id, string.Empty), r.Message, r.Level, r.RuleIds.ToArray())).ToList(),
        };

        private static ApiCheckRunDelta materialise(ApiCheckRunDelta delta) => new ApiCheckRunDelta(delta.PreviousRunAt, delta.CurrentRunAt,
            delta.NewIssues.ToArray(), delta.ResolvedIssues.ToArray(), delta.WorsenedIssues.ToArray(), delta.ImprovedIssues.ToArray(), delta.UnchangedIssues.ToArray());
    }

    public class VerifierCategory
    {
        public string Name = string.Empty;
        public bool IsGeneral;
        public ulong? BeatmapId;
        public MvBeatmap.Mode? Mode;
        public MvBeatmap.Difficulty? DetectedLevel;

        /// <summary>
        /// The difficulty level which the difficulty is interpreted as.
        /// </summary>
        public MvBeatmap.Difficulty? Level;

        public double? StarRating;
        public List<VerifierIssue> Issues = new List<VerifierIssue>();

        /// <summary>
        /// Whether the difficulty is being checked again with a different difficulty level.
        /// </summary>
        public volatile bool IsReinterpreting;

        /// <summary>
        /// The most severe level of the issues, which is <see cref="Issue.Level.Check"/> if there are none.
        /// </summary>
        public Issue.Level HighestLevel => Issues.Count == 0 ? Issue.Level.Check : Issues.Max(i => i.Level);
    }

    public record VerifierIssue(int CheckId, string CheckName, string Message, Issue.Level Level, IReadOnlyList<string> RuleIds);
}
