// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
        /// The length of the sections of <see cref="EditorBeatmapDifficultyInfo.Strains"/>, in milliseconds.
        /// The same as the strain sections of difficulty calculation, osu!stable and McOsu.
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
            var sampler = new StrainSampler();
            var attributes = calculator.CalculateWithSkills(out var skills, out var difficultyHitObjects, sampler.SampleBefore, cancellationToken);
            var graphSkills = calculator.GetStrainGraphSkills(skills, attributes).ToArray();

            float[] strains = sampler.CreateStrains(graphSkills, difficultyHitObjects, out double strainsStartTime);

            return new EditorBeatmapDifficultyInfo(attributes.StarRating, attributes.MaxCombo, strains, strainsStartTime);
        }

        /// <summary>
        /// Collects the strain of skills over time, in sections of <see cref="STRAIN_SECTION_LENGTH"/> (like McOsu).
        /// </summary>
        /// <remarks>
        /// The sections are aligned like in difficulty calculation: the first one ends at the first multiple of <see cref="STRAIN_SECTION_LENGTH"/> at or after the first object.
        /// Each section takes the highest strain of its objects and the strain of the previous object decayed until the start of the section,
        /// so that sections without objects (e.g. during long sliders) don't drop to zero.
        /// Skills which don't provide a decaying strain only contribute the difficulty of their objects.
        /// </remarks>
        private class StrainSampler
        {
            /// <summary>
            /// The decayed strain at the start of sections, for each skill.
            /// </summary>
            private readonly Dictionary<Skill, List<(int Section, double Strain)>> decayedStrains = new Dictionary<Skill, List<(int, double)>>();

            private double firstSectionEnd;
            private double nextSectionEnd;
            private bool started;

            public void SampleBefore(Skill[] skills, DifficultyHitObject next)
            {
                if (!started)
                {
                    started = true;
                    firstSectionEnd = nextSectionEnd = Math.Ceiling(next.StartTime / STRAIN_SECTION_LENGTH) * STRAIN_SECTION_LENGTH;
                    return;
                }

                while (next.StartTime > nextSectionEnd)
                {
                    // a new section starts at the end of the current one.
                    int section = sectionEndingAt(nextSectionEnd + STRAIN_SECTION_LENGTH);

                    foreach (var skill in skills)
                    {
                        double? strain = skill switch
                        {
                            StrainSkill strainSkill => strainSkill.GetStrainBefore(nextSectionEnd, next),
                            VariableLengthStrainSkill variableLengthStrainSkill => variableLengthStrainSkill.GetStrainBefore(nextSectionEnd, next),
                            _ => null,
                        };

                        if (strain == null)
                            continue;

                        if (!decayedStrains.TryGetValue(skill, out var list))
                            decayedStrains[skill] = list = new List<(int, double)>();

                        list.Add((section, strain.Value));
                    }

                    nextSectionEnd += STRAIN_SECTION_LENGTH;
                }
            }

            /// <summary>
            /// The index of the section which ends at or after the given time.
            /// </summary>
            private int sectionEndingAt(double time) => Math.Max(0, (int)Math.Ceiling((time - firstSectionEnd) / STRAIN_SECTION_LENGTH - 1e-9));

            /// <summary>
            /// Combines the strains of the given skills, relative to the hardest section.
            /// Each skill is relative to its own hardest section, as the values of different skills have different scales.
            /// </summary>
            /// <param name="graphSkills">The skills to combine, along with how much each contributes.</param>
            /// <param name="difficultyHitObjects">The processed objects.</param>
            /// <param name="startTime">The start time of the first section.</param>
            public float[] CreateStrains((Skill Skill, double Weight)[] graphSkills, DifficultyHitObject[] difficultyHitObjects, out double startTime)
            {
                startTime = firstSectionEnd - STRAIN_SECTION_LENGTH;

                if (difficultyHitObjects.Length == 0)
                    return Array.Empty<float>();

                int sectionCount = sectionEndingAt(difficultyHitObjects.Max(h => h.StartTime)) + 1;
                double[] combined = new double[sectionCount];

                foreach (var (skill, weight) in graphSkills)
                {
                    if (weight <= 0 || !double.IsFinite(weight))
                        continue;

                    double[] sections = new double[sectionCount];

                    var difficulties = skill.GetObjectDifficulties();

                    for (int i = 0; i < Math.Min(difficulties.Count, difficultyHitObjects.Length); i++)
                        add(sectionEndingAt(difficultyHitObjects[i].StartTime), difficulties[i]);

                    if (decayedStrains.TryGetValue(skill, out var decayed))
                    {
                        foreach (var (section, strain) in decayed)
                            add(section, strain);
                    }

                    double max = sections.Max();

                    if (max <= 0)
                        continue;

                    for (int i = 0; i < sectionCount; i++)
                        combined[i] += sections[i] / max * weight;

                    void add(int section, double strain)
                    {
                        if (section < sectionCount && double.IsFinite(strain))
                            sections[section] = Math.Max(sections[section], strain);
                    }
                }

                double highest = combined.Max();

                return combined.Select(c => highest > 0 ? (float)(c / highest) : 0).ToArray();
            }
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
    /// The strain over time, in consecutive sections of <see cref="EditorBeatmapDifficulty.STRAIN_SECTION_LENGTH"/> starting at <paramref name="StrainsStartTime"/>,
    /// relative to the hardest section (from 0 to 1).
    /// </param>
    /// <param name="StrainsStartTime">The start time of the first section of <paramref name="Strains"/>.</param>
    public record EditorBeatmapDifficultyInfo(double StarRating, int MaxCombo, float[] Strains, double StrainsStartTime);
}
