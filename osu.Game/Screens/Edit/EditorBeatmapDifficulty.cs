// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Calculates the difficulty of the beatmap being edited, and recalculates it whenever the beatmap changes.
    /// </summary>
    public partial class EditorBeatmapDifficulty : Component
    {
        /// <summary>
        /// The delay after a change before recalculating, so that consecutive changes (e.g. while dragging objects) don't each cause a calculation.
        /// </summary>
        private const double recalculation_delay = 300;

        /// <summary>
        /// The difficulty of the beatmap, or <c>null</c> if not calculated (yet).
        /// </summary>
        public IBindable<EditorBeatmapDifficultyInfo?> Info => info;

        private readonly Bindable<EditorBeatmapDifficultyInfo?> info = new Bindable<EditorBeatmapDifficultyInfo?>();

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler? changeHandler { get; set; }

        private ScheduledDelegate? scheduledRecalculation;
        private CancellationTokenSource? cancellationSource;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // the beatmap is passed to the calculation in its legacy format, which is only supported for legacy rulesets.
            if (editorBeatmap.BeatmapInfo.Ruleset.CreateInstance() is not ILegacyRuleset)
                return;

            if (changeHandler != null)
                changeHandler.OnStateChange += scheduleRecalculation;

            recalculate();
        }

        private void scheduleRecalculation()
        {
            scheduledRecalculation?.Cancel();
            scheduledRecalculation = Scheduler.AddDelayed(recalculate, recalculation_delay);
        }

        private void recalculate()
        {
            cancellationSource?.Cancel();
            var source = cancellationSource = new CancellationTokenSource();

            // the beatmap is serialised on the update thread, so that the calculation doesn't access objects which may be changed concurrently.
            byte[] state;

            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                    new LegacyBeatmapEncoder(editorBeatmap, editorBeatmap.BeatmapSkin, null).Encode(writer);

                state = stream.ToArray();
            }

            var rulesetInfo = editorBeatmap.BeatmapInfo.Ruleset;

            Task.Run(() => calculate(state, rulesetInfo, source.Token), source.Token).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Logger.Log($"Failed to calculate the difficulty of the beatmap in the editor: {task.Exception}", level: LogLevel.Debug);
                    return;
                }

                if (!task.IsCompletedSuccessfully)
                    return;

                Schedule(() =>
                {
                    if (!source.IsCancellationRequested)
                        info.Value = task.GetResultSafely();
                });
            }, CancellationToken.None);
        }

        private static EditorBeatmapDifficultyInfo calculate(byte[] state, RulesetInfo rulesetInfo, CancellationToken cancellationToken)
        {
            Beatmap beatmap;

            using (var stream = new MemoryStream(state))
            using (var reader = new LineBufferedReader(stream))
                beatmap = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);

            beatmap.BeatmapInfo.Ruleset = rulesetInfo;

            var calculator = rulesetInfo.CreateInstance().CreateDifficultyCalculator(new FlatWorkingBeatmap(beatmap));
            var attributes = calculator.Calculate(cancellationToken);

            return new EditorBeatmapDifficultyInfo(attributes.StarRating, attributes.MaxCombo);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            cancellationSource?.Cancel();

            if (changeHandler != null)
                changeHandler.OnStateChange -= scheduleRecalculation;
        }
    }

    /// <summary>
    /// The difficulty of a beatmap being edited.
    /// </summary>
    /// <param name="StarRating">The star rating.</param>
    /// <param name="MaxCombo">The maximum achievable combo.</param>
    public record EditorBeatmapDifficultyInfo(double StarRating, int MaxCombo);
}
