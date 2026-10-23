// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Configuration;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.OSD;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Toggles the samples at the end of the selected objects with a duration (e.g. slider tails and spinner ends) between being muted and having the volume of the object.
    /// </summary>
    public partial class EditorObjectEndMuting : Component, IKeyBindingHandler<GlobalAction>
    {
        /// <summary>
        /// The volume of muted object ends. Samples with this volume or less are considered muted.
        /// </summary>
        public const int MUTED_VOLUME = 5;

        /// <summary>
        /// The custom sample index which is used for muted object ends if <see cref="OsuSetting.SlopEditorMutedEndsSoftC999"/> is enabled.
        /// </summary>
        public const string MUTED_SUFFIX = "999";

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private OnScreenDisplay? onScreenDisplay { get; set; }

        private Bindable<bool> softC999 = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            softC999 = config.GetBindable<bool>(OsuSetting.SlopEditorMutedEndsSoftC999);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat || e.Action != GlobalAction.EditorMuteObjectEnds)
                return false;

            var objects = editorBeatmap.SelectedHitObjects.Where(h => h is IHasDuration && getEndSamples(h).Count > 0).ToList();

            if (objects.Count == 0)
                return false;

            editorBeatmap.BeginChange();

            foreach (var hitObject in objects)
            {
                var endSamples = getEndSamples(hitObject);

                setEndSamples(hitObject, endSamples.All(s => s.Volume <= MUTED_VOLUME)
                    ? Unmute(endSamples, getReferenceSamples(hitObject))
                    : Mute(endSamples, softC999.Value));

                editorBeatmap.Update(hitObject);
            }

            editorBeatmap.EndChange();

            onScreenDisplay?.Display(new MuteToast(objects.Count));
            return true;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        /// <summary>
        /// Mutes <paramref name="samples"/>, optionally by switching them to the soft:C999 custom sample set.
        /// </summary>
        public static List<HitSampleInfo> Mute(IList<HitSampleInfo> samples, bool softC999)
        {
            if (!softC999)
                return samples.Select(s => s.With(newVolume: MUTED_VOLUME)).ToList();

            return samples.Select(s => s.With(newBank: HitSampleInfo.BANK_SOFT, newSuffix: MUTED_SUFFIX, newVolume: MUTED_VOLUME, newUseBeatmapSamples: true)).ToList();
        }

        /// <summary>
        /// Restores muted <paramref name="samples"/> to the volume (and custom sample set) of <paramref name="referenceSamples"/>.
        /// </summary>
        public static List<HitSampleInfo> Unmute(IList<HitSampleInfo> samples, IList<HitSampleInfo>? referenceSamples)
        {
            int volume = referenceSamples?.Count > 0 ? referenceSamples.Max(s => s.Volume) : 100;

            if (volume <= MUTED_VOLUME)
                volume = 100;

            return samples.Select(s =>
            {
                // muted with the custom sample set, which is restored along with the volume.
                if (s.Suffix == MUTED_SUFFIX)
                {
                    var reference = referenceSamples?.FirstOrDefault(r => r.Name == s.Name)
                                    ?? referenceSamples?.FirstOrDefault(r => r.Name == HitSampleInfo.HIT_NORMAL);

                    return reference != null
                        ? s.With(newBank: reference.Bank, newSuffix: reference.Suffix, newVolume: volume, newUseBeatmapSamples: reference.UseBeatmapSamples)
                        : s.With(newBank: HitSampleInfo.BANK_NORMAL, newSuffix: null, newVolume: volume, newUseBeatmapSamples: false);
                }

                return s.With(newVolume: volume);
            }).ToList();
        }

        private static IList<HitSampleInfo> getEndSamples(HitObject hitObject)
        {
            if (hitObject is IHasRepeats repeats)
                return repeats.NodeSamples.Count > 0 ? repeats.NodeSamples[^1] : new List<HitSampleInfo>();

            // objects without nodes (e.g. spinners) play their samples at their end.
            return hitObject.Samples;
        }

        private static void setEndSamples(HitObject hitObject, List<HitSampleInfo> samples)
        {
            if (hitObject is IHasRepeats repeats)
                repeats.NodeSamples[^1] = samples;
            else
                hitObject.Samples = samples;
        }

        /// <summary>
        /// The samples whose volume is restored when unmuting the end of <paramref name="hitObject"/>.
        /// </summary>
        private IList<HitSampleInfo>? getReferenceSamples(HitObject hitObject)
        {
            if (hitObject is IHasRepeats repeats && repeats.NodeSamples.Count > 1)
                return repeats.NodeSamples[0];

            return editorBeatmap.HitObjects.LastOrDefault(h => h.StartTime < hitObject.StartTime)?.Samples;
        }

        private partial class MuteToast : Toast
        {
            public MuteToast(int count)
                : base(SlopEditorStrings.MuteObjectEnds, SlopEditorStrings.ToggledMutedEnds(count))
            {
            }

            [BackgroundDependencyLoader]
            private void load(RealmKeyBindingStore keyBindingStore)
            {
                ExtraText = keyBindingStore.GetBindingsStringFor(GlobalAction.EditorMuteObjectEnds);
            }
        }
    }
}
