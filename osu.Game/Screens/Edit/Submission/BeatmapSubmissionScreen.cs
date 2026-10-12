// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Development;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO.Archives;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Edit.Hitsounding;
using osu.Game.Screens.Select;
using osuTK;

namespace osu.Game.Screens.Edit.Submission
{
    public partial class BeatmapSubmissionScreen : OsuScreen
    {
        private BeatmapSubmissionOverlay overlay = null!;

        public override bool DisallowExternalBeatmapRulesetChanges => true;

        protected override bool InitialBackButtonVisibility => false;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [Resolved]
        private RealmAccess realmAccess { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuConfigManager configManager { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Cached]
        private BeatmapSubmissionSettings settings { get; } = new BeatmapSubmissionSettings();

        private Container submissionProgress = null!;
        private SubmissionStageProgress exportStep = null!;
        private SubmissionStageProgress createSetStep = null!;
        private SubmissionStageProgress uploadStep = null!;
        private SubmissionStageProgress updateStep = null!;
        private Container successContainer = null!;
        private Container flashLayer = null!;

        private uint? beatmapSetId;
        private MemoryStream? beatmapPackageStream;

        private ProgressNotification? exportProgressNotification;
        private ProgressNotification? updateProgressNotification;

        private Live<BeatmapSetInfo>? importedSet;

        private Sample completedSample = null!;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            // hitsound difficulties only exist to edit hitsounds, so they aren't uploaded unless chosen otherwise in the submission settings.
            // set before the settings are loaded, as they show the defaults.
            settings.ExcludeHitsoundDifficulties(configManager, Beatmap.Value.BeatmapSetInfo.Beatmaps);

            AddRangeInternal(new Drawable[]
            {
                overlay = new BeatmapSubmissionOverlay(),
                submissionProgress = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    AutoSizeDuration = 400,
                    AutoSizeEasing = Easing.OutQuint,
                    Alpha = 0,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 0.6f,
                    Masking = true,
                    CornerRadius = 10,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background5,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding(20),
                            Spacing = new Vector2(5),
                            Children = new Drawable[]
                            {
                                createSetStep = new SubmissionStageProgress
                                {
                                    StageDescription = BeatmapSubmissionStrings.Preparing,
                                    StageIndex = 0,
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                },
                                exportStep = new SubmissionStageProgress
                                {
                                    StageDescription = BeatmapSubmissionStrings.Exporting,
                                    StageIndex = 1,
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                },
                                uploadStep = new SubmissionStageProgress
                                {
                                    StageDescription = BeatmapSubmissionStrings.Uploading,
                                    StageIndex = 2,
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                },
                                updateStep = new SubmissionStageProgress
                                {
                                    StageDescription = BeatmapSubmissionStrings.Finishing,
                                    StageIndex = 3,
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                },
                                successContainer = new Container
                                {
                                    Padding = new MarginPadding(20),
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    AutoSizeAxes = Axes.Both,
                                    CornerRadius = BeatmapCard.CORNER_RADIUS,
                                    Child = flashLayer = new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Masking = true,
                                        CornerRadius = BeatmapCard.CORNER_RADIUS,
                                        Depth = float.MinValue,
                                        Alpha = 0,
                                        Child = new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                        }
                                    }
                                },
                            }
                        }
                    }
                }
            });

            overlay.State.BindValueChanged(_ =>
            {
                if (overlay.State.Value == Visibility.Hidden)
                {
                    if (!overlay.Completed)
                    {
                        allowExit();
                        this.Exit();
                    }
                    else
                    {
                        submissionProgress.FadeIn(200, Easing.OutQuint);
                        createBeatmapSet();
                    }
                }
            });

            completedSample = audio.Samples.Get(@"UI/bss-complete");

            if (Beatmap.Value.BeatmapSetInfo.OnlineID > 0)
            {
                var req = new GetBeatmapSetRequest(Beatmap.Value.BeatmapSetInfo.OnlineID);
                api.Queue(req);
                settings.LatestOnlineStateRequest = req;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            configManager.BindWith(OsuSetting.EditorSubmissionNotifyOnDiscussionReplies, settings.NotifyOnDiscussionReplies);
        }

        /// <summary>
        /// The difficulties which aren't uploaded (see <see cref="BeatmapSubmissionSettings.ExcludedBeatmaps"/>). They are kept locally.
        /// </summary>
        private List<BeatmapInfo> excludedBeatmaps = new List<BeatmapInfo>();

        private HashSet<string> excludedFilenames = new HashSet<string>();

        private IEnumerable<BeatmapInfo> uploadedBeatmaps => Beatmap.Value.BeatmapSetInfo.Beatmaps.Where(b => excludedBeatmaps.All(e => e.ID != b.ID));

        private void createBeatmapSet()
        {
            bool beatmapHasOnlineId = Beatmap.Value.BeatmapSetInfo.OnlineID > 0;

            excludedBeatmaps = Beatmap.Value.BeatmapSetInfo.Beatmaps.Where(b => settings.ExcludedBeatmaps.Any(e => e.ID == b.ID)).ToList();

            // a beatmap set needs at least one difficulty (the settings don't allow excluding all of them, but this is a safety net).
            if (excludedBeatmaps.Count == Beatmap.Value.BeatmapSetInfo.Beatmaps.Count)
                excludedBeatmaps.Clear();

            excludedFilenames = excludedBeatmaps.Select(b => b.File?.Filename).OfType<string>().ToHashSet();

            foreach (var excluded in excludedBeatmaps)
                log($@"not uploading difficulty ""{excluded.DifficultyName}""");

            PutBeatmapSetRequest createRequest;

            if (beatmapHasOnlineId)
            {
                createRequest = PutBeatmapSetRequest.UpdateExisting(
                    (uint)Beatmap.Value.BeatmapSetInfo.OnlineID,
                    uploadedBeatmaps.Where(b => b.OnlineID > 0).Select(b => (uint)b.OnlineID).ToArray(),
                    (uint)uploadedBeatmaps.Count(b => b.OnlineID <= 0),
                    settings);
                log($"Updating existing beatmap set (id:{createRequest.BeatmapSetID} beatmapsToKeep:[{string.Join(",", createRequest.BeatmapsToKeep)}] beatmapsToCreate:{createRequest.BeatmapsToCreate})");
            }
            else
            {
                createRequest = PutBeatmapSetRequest.CreateNew((uint)uploadedBeatmaps.Count(), settings);
                log($"Creating new beatmap set (beatmapsToCreate:{createRequest.BeatmapsToCreate})");
            }

            createRequest.Success += async response =>
            {
                createSetStep.SetCompleted();
                beatmapSetId = response.BeatmapSetId;

                // at this point the set has an assigned online ID.
                // it's important to proactively store it to the realm database,
                // so that in the event in further failures in the process, the online ID is not lost.
                // losing it can incur creation of redundant new sets server-side, or even cause online ID confusion.
                if (!beatmapHasOnlineId)
                {
                    await realmAccess.WriteAsync(r =>
                    {
                        var refetchedSet = r.Find<BeatmapSetInfo>(Beatmap.Value.BeatmapSetInfo.ID);
                        refetchedSet!.OnlineID = (int)beatmapSetId.Value;
                    }).ConfigureAwait(true);
                }

                await createBeatmapPackage(response).ConfigureAwait(true);
            };
            createRequest.Failure += ex =>
            {
                createSetStep.SetFailed(ex.Message);
                log($"Beatmap set creation/update failed: {ex}");
                allowExit();
            };

            createSetStep.SetInProgress();
            api.Queue(createRequest);
        }

        private async Task createBeatmapPackage(PutBeatmapSetResponse response)
        {
            Debug.Assert(ThreadSafety.IsUpdateThread);

            exportStep.SetInProgress();

            try
            {
                beatmapPackageStream = new MemoryStream();
                exportProgressNotification = new ProgressNotification();

                var legacyBeatmapExporter = new SubmissionBeatmapExporter(storage, response, excludedBeatmaps.Select(b => b.Hash).ToHashSet());

                await legacyBeatmapExporter
                      .ExportToStreamAsync(Beatmap.Value.BeatmapSetInfo.ToLive(realmAccess), beatmapPackageStream, exportProgressNotification)
                      .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                exportStep.SetFailed(ex.Message);
                exportProgressNotification = null;
                log($"Export failed: {ex}");
                allowExit();
                return;
            }

            exportStep.SetCompleted();
            exportProgressNotification = null;

            await Task.Delay(200).ConfigureAwait(true);

            if (response.Files.Count > 0)
                await patchBeatmapSet(response.Files).ConfigureAwait(true);
            else
                replaceBeatmapSet();
        }

        private async Task patchBeatmapSet(ICollection<BeatmapSetFile> onlineFiles)
        {
            Debug.Assert(beatmapSetId != null);
            Debug.Assert(beatmapPackageStream != null);
            log("Determining list of files to patch...");

            var onlineFilesByFilename = onlineFiles.ToDictionary(f => f.Filename, f => f.SHA2Hash);

            // disposing the `ArchiveReader` makes the underlying stream no longer readable which we don't want.
            // make a local copy to defend against it.
            using var archiveReader = new ZipArchiveReader(new MemoryStream(beatmapPackageStream.ToArray()));
            var filesToUpdate = new HashSet<string>();

            foreach (string filename in archiveReader.Filenames)
            {
                // difficulties which aren't uploaded are deleted online if they have been uploaded before.
                if (excludedFilenames.Contains(filename))
                    continue;

                string localHash = archiveReader.GetStream(filename).ComputeSHA2Hash();

                if (!onlineFilesByFilename.Remove(filename, out string? onlineHash))
                {
                    log($@"new file: {filename}");
                    filesToUpdate.Add(filename);
                    continue;
                }

                if (!localHash.Equals(onlineHash, StringComparison.OrdinalIgnoreCase))
                {
                    log($@"changed file: {filename} (localHash:{localHash} onlineHash:{onlineHash})");
                    filesToUpdate.Add(filename);
                }
            }

            var changedFiles = new Dictionary<string, byte[]>();

            foreach (string file in filesToUpdate)
                changedFiles.Add(file, await archiveReader.GetStream(file).ReadAllBytesToArrayAsync().ConfigureAwait(true));

            var patchRequest = new PatchBeatmapPackageRequest(beatmapSetId.Value);
            patchRequest.FilesChanged.AddRange(changedFiles);
            patchRequest.FilesDeleted.AddRange(onlineFilesByFilename.Keys);

            foreach (string file in patchRequest.FilesDeleted)
                log($@"deleted file: {file}");

            patchRequest.Success += uploadCompleted;
            patchRequest.Failure += ex =>
            {
                uploadStep.SetFailed(ex.Message);
                log($"Upload failed: {ex}");
                allowExit();
            };
            patchRequest.Progressed += (current, total) => uploadStep.SetInProgress(total > 0 ? (float)current / total : null);

            api.Queue(patchRequest);
            uploadStep.SetInProgress();
        }

        private void replaceBeatmapSet()
        {
            log("Peforming full package upload...");

            Debug.Assert(beatmapSetId != null);
            Debug.Assert(beatmapPackageStream != null);

            var uploadRequest = new ReplaceBeatmapPackageRequest(beatmapSetId.Value, getUploadedPackage());

            uploadRequest.Success += uploadCompleted;
            uploadRequest.Failure += ex =>
            {
                uploadStep.SetFailed(ex.Message);
                log($"Full package upload failed: {ex}");
                allowExit();
            };
            uploadRequest.Progressed += (current, total) => uploadStep.SetInProgress((float)current / Math.Max(total, 1));

            api.Queue(uploadRequest);
            uploadStep.SetInProgress();
        }

        /// <summary>
        /// The exported package without the difficulties which aren't uploaded.
        /// </summary>
        private byte[] getUploadedPackage()
        {
            Debug.Assert(beatmapPackageStream != null);

            if (excludedFilenames.Count == 0)
                return beatmapPackageStream.ToArray();

            using var source = new ZipArchive(new MemoryStream(beatmapPackageStream.ToArray()), ZipArchiveMode.Read);
            var output = new MemoryStream();

            using (var target = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                foreach (var entry in source.Entries)
                {
                    if (excludedFilenames.Contains(entry.FullName))
                        continue;

                    var copy = target.CreateEntry(entry.FullName);
                    copy.LastWriteTime = entry.LastWriteTime;

                    using var from = entry.Open();
                    using var to = copy.Open();
                    from.CopyTo(to);
                }
            }

            return output.ToArray();
        }

        private void uploadCompleted()
        {
            uploadStep.SetCompleted();
            updateLocalBeatmap().ConfigureAwait(true);
        }

        private async Task updateLocalBeatmap()
        {
            log(@"Updating local beatmap set...");

            Debug.Assert(beatmapSetId != null);
            Debug.Assert(beatmapPackageStream != null);

            updateStep.SetInProgress();
            await Task.Delay(200).ConfigureAwait(true);

            try
            {
                importedSet = await beatmaps.ImportAsUpdate(
                    updateProgressNotification = new ProgressNotification(),
                    new ImportTask(beatmapPackageStream, $"{beatmapSetId}.osz"),
                    Beatmap.Value.BeatmapSetInfo).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                updateStep.SetFailed(ex.Message);
                log($@"Local update failed: {ex}");
                allowExit();
                return;
            }

            // the difficulties are recreated by the import, so the hitsound difficulties are marked as such again in case they aren't named like one.
            if (importedSet != null)
            {
                foreach (var excluded in excludedBeatmaps.Where(b => HitsoundPreferences.IsHitsoundDifficulty(configManager, b)))
                {
                    var imported = importedSet.PerformRead(s => s.Beatmaps.FirstOrDefault(b => b.DifficultyName == excluded.DifficultyName)?.Detach());

                    if (imported != null)
                        HitsoundPreferences.SetHitsoundDifficulty(configManager, imported, true);
                }
            }

            updateStep.SetCompleted();
            showBeatmapCard();
            allowExit();

            if (configManager.Get<bool>(OsuSetting.EditorSubmissionLoadInBrowserAfterSubmission))
            {
                await Task.Delay(1000).ConfigureAwait(true);
                game?.OpenUrlExternally($"{api.Endpoints.WebsiteUrl}/beatmapsets/{beatmapSetId}");
            }
        }

        private void showBeatmapCard()
        {
            Debug.Assert(beatmapSetId != null);

            var getBeatmapSetRequest = new GetBeatmapSetRequest((int)beatmapSetId.Value);
            getBeatmapSetRequest.Success += beatmapSet =>
            {
                LoadComponentAsync(new BeatmapCardExtra(beatmapSet, false), loaded =>
                {
                    successContainer.Add(loaded);
                    flashLayer.FadeOutFromOne(2000, Easing.OutQuint);
                });

                completedSample.Play();
            };

            api.Queue(getBeatmapSetRequest);
        }

        private void allowExit()
        {
            BackButtonVisibility.Value = true;
        }

        protected override void Update()
        {
            base.Update();

            if (exportProgressNotification != null && exportProgressNotification.Ongoing)
                exportStep.SetInProgress(exportProgressNotification.Progress);

            if (updateProgressNotification != null && updateProgressNotification.Ongoing)
                updateStep.SetInProgress(updateProgressNotification.Progress);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            // We probably want a method of cancelling in the future…
            if (!BackButtonVisibility.Value)
                return true;

            if (importedSet != null)
            {
                game?.PerformFromScreen(s =>
                {
                    if (s is OsuScreen osuScreen)
                    {
                        Debug.Assert(importedSet != null);
                        var targetBeatmap = importedSet.Value.Beatmaps.FirstOrDefault(b => b.DifficultyName == Beatmap.Value.BeatmapInfo.DifficultyName)
                                            ?? importedSet.Value.Beatmaps.First();
                        osuScreen.Beatmap.Value = beatmaps.GetWorkingBeatmap(targetBeatmap);
                    }

                    s.Push(new EditorLoader());
                }, [typeof(SongSelect)]);

                return false;
            }

            return base.OnExiting(e);
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            overlay.Show();
        }

        private static void log(string message)
            => Logger.Log($@"[{nameof(BeatmapSubmissionScreen)}] {message}", LoggingTarget.Database);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            beatmapPackageStream?.Dispose();
        }
    }
}
