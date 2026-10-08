// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
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
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Calculates the difficulty of the beatmap being edited, and recalculates it whenever the beatmap changes.
    /// </summary>
    public partial class EditorBeatmapDifficulty : Component
    {
        /// <summary>
        /// The length of the sections which <see cref="EditorBeatmapDifficultyInfo.Strains"/> are combined in, in milliseconds.
        /// The same as the strain sections used by most difficulty calculations.
        /// </summary>
        public const double STRAIN_SECTION_LENGTH = 400;

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
            var attributes = calculator.CalculateWithSkills(out var skills, out var difficultyHitObjects, cancellationToken);

            return new EditorBeatmapDifficultyInfo(attributes.StarRating, attributes.MaxCombo, calculateStrains(skills, difficultyHitObjects));
        }

        /// <summary>
        /// Combines the difficulty of the processed objects of all skills into sections of <see cref="STRAIN_SECTION_LENGTH"/>, relative to the hardest section.
        /// </summary>
        /// <remarks>
        /// Each skill is relative to its own hardest object, as the values of different skills have different scales.
        /// A section takes the highest value of any skill at any object in it.
        /// </remarks>
        private static float[] calculateStrains(Skill[] skills, DifficultyHitObject[] difficultyHitObjects)
        {
            if (difficultyHitObjects.Length == 0)
                return Array.Empty<float>();

            double lastTime = difficultyHitObjects.Max(h => h.BaseObject.StartTime);

            if (lastTime < 0)
                return Array.Empty<float>();

            float[] strains = new float[(int)(lastTime / STRAIN_SECTION_LENGTH) + 1];

            foreach (var skill in skills)
            {
                var difficulties = skill.GetObjectDifficulties();
                int count = Math.Min(difficulties.Count, difficultyHitObjects.Length);

                double max = 0;

                for (int i = 0; i < count; i++)
                    max = Math.Max(max, difficulties[i]);

                if (max <= 0 || !double.IsFinite(max))
                    continue;

                for (int i = 0; i < count; i++)
                {
                    double time = difficultyHitObjects[i].BaseObject.StartTime;

                    if (time < 0 || !double.IsFinite(difficulties[i]))
                        continue;

                    int section = (int)(time / STRAIN_SECTION_LENGTH);
                    strains[section] = Math.Max(strains[section], (float)(difficulties[i] / max));
                }
            }

            return strains;
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
    /// <param name="Strains">
    /// The strain over time, in consecutive sections of <see cref="EditorBeatmapDifficulty.STRAIN_SECTION_LENGTH"/> starting at time 0,
    /// relative to the hardest section (from 0 to 1).
    /// </param>
    public record EditorBeatmapDifficultyInfo(double StarRating, int MaxCombo, float[] Strains);
}
