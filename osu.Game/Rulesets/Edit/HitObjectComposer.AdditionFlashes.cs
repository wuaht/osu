// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Edit.Components.TernaryButtons;

namespace osu.Game.Rulesets.Edit
{
    public abstract partial class HitObjectComposer<TObject, TAction>
    {
        #region Addition flashes

        /// <summary>
        /// Playback jumps longer than this (in milliseconds of track time) are considered seeks, which don't flash anything.
        /// </summary>
        private const double max_addition_flash_interval = 500;

        private Bindable<bool> flashAdditions = null!;

        private double? lastAdditionFlashTime;

        private void loadAdditionFlashes(OsuConfigManager config)
        {
            flashAdditions = config.GetBindable<bool>(OsuSetting.SlopEditorFlashAdditions);
        }

        /// <summary>
        /// Flashes the addition buttons (whistle, finish, clap) whenever a hit object node with that addition is passed during playback (like osu!stable).
        /// </summary>
        private void updateAdditionFlashes()
        {
            double currentTime = EditorClock.CurrentTime;
            double? lastTime = lastAdditionFlashTime;
            lastAdditionFlashTime = currentTime;

            if (!flashAdditions.Value || !EditorClock.IsRunning || lastTime == null)
                return;

            double elapsed = currentTime - lastTime.Value;

            if (elapsed <= 0 || elapsed > max_addition_flash_interval)
                return;

            var additions = GetPassedSampleNames(EditorBeatmap.HitObjects, lastTime.Value, currentTime);

            if (additions.Count == 0)
                return;

            foreach (var button in togglesCollection.ChildrenOfType<DrawableTernaryButton>())
            {
                foreach (var (name, state) in SelectionSampleStates)
                {
                    if (ReferenceEquals(button.Current, state) && additions.Contains(name))
                        button.Flash();
                }
            }
        }

        /// <summary>
        /// Returns the names of the addition samples (<see cref="HitSampleInfo.ALL_ADDITIONS"/>) of all hit object nodes in the time range (<paramref name="fromTime"/>, <paramref name="toTime"/>].
        /// </summary>
        public static HashSet<string> GetPassedSampleNames(IEnumerable<HitObject> hitObjects, double fromTime, double toTime)
        {
            var names = new HashSet<string>();

            foreach (var hitObject in hitObjects)
            {
                if (hitObject.StartTime > toTime || hitObject.GetEndTime() <= fromTime)
                    continue;

                foreach (var (time, samples) in getNodes(hitObject))
                {
                    if (time <= fromTime || time > toTime)
                        continue;

                    foreach (var sample in samples)
                    {
                        if (HitSampleInfo.ALL_ADDITIONS.Contains(sample.Name))
                            names.Add(sample.Name);
                    }
                }
            }

            return names;
        }

        private static IEnumerable<(double time, IList<HitSampleInfo> samples)> getNodes(HitObject hitObject)
        {
            switch (hitObject)
            {
                case IHasRepeats hasRepeats:
                    double spanDuration = hasRepeats.Duration / Math.Max(1, hasRepeats.SpanCount());

                    for (int i = 0; i <= hasRepeats.SpanCount(); i++)
                        yield return (hitObject.StartTime + spanDuration * i, i < hasRepeats.NodeSamples.Count ? hasRepeats.NodeSamples[i] : hitObject.Samples);

                    break;

                // e.g. spinners, which play their samples at their end.
                case IHasDuration hasDuration:
                    yield return (hasDuration.EndTime, hitObject.Samples);

                    break;

                default:
                    yield return (hitObject.StartTime, hitObject.Samples);

                    break;
            }
        }

        #endregion
    }
}
