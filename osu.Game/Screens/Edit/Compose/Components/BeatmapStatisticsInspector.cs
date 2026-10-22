// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.Compose.Components
{
    /// <summary>
    /// Displays general statistics of the beatmap being edited: its difficulty name, max combo, star rating, the timing at the current time
    /// and the distance snap of the current object to its previous object (like osu!stable).
    /// </summary>
    public partial class BeatmapStatisticsInspector : EditorInspector
    {
        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private EditorBeatmapDifficulty? beatmapDifficulty { get; set; }

        [Resolved]
        private IDistanceSnapProvider? distanceSnapProvider { get; set; }

        private readonly IBindable<EditorBeatmapDifficultyInfo?> difficultyInfo = new Bindable<EditorBeatmapDifficultyInfo?>();

        private string lastDifficultyName = string.Empty;
        private double lastBPM;
        private TimeSignature? lastTimeSignature;
        private string lastDistanceSnap = "-";

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (beatmapDifficulty != null)
                difficultyInfo.BindTo(beatmapDifficulty.Info);

            difficultyInfo.BindValueChanged(_ => updateText());

            updateTiming();
            updateText();
        }

        protected override void Update()
        {
            base.Update();

            // the difficulty name may be changed in the setup screen.
            if (updateTiming() | updateDistanceSnap() | EditorBeatmap.BeatmapInfo.DifficultyName != lastDifficultyName)
                updateText();
        }

        /// <summary>
        /// Updates the timing at the current time.
        /// </summary>
        /// <returns>Whether the timing changed.</returns>
        private bool updateTiming()
        {
            TimingControlPoint timingPoint = EditorBeatmap.ControlPointInfo.TimingPointAt(editorClock.CurrentTime);

            if (timingPoint.BPM == lastBPM && timingPoint.TimeSignature.Equals(lastTimeSignature!))
                return false;

            lastBPM = timingPoint.BPM;
            lastTimeSignature = timingPoint.TimeSignature;
            return true;
        }

        /// <summary>
        /// Updates the distance snap between the selected object (or the last object at the current time) and its previous object.
        /// Unlike the distance snap shown while placing objects, this doesn't depend on the cursor.
        /// </summary>
        /// <returns>Whether the displayed distance snap changed.</returns>
        private bool updateDistanceSnap()
        {
            string distanceSnap = getDistanceSnap() is double snap ? $"{snap:N2}x" : "-";

            if (distanceSnap == lastDistanceSnap)
                return false;

            lastDistanceSnap = distanceSnap;
            return true;
        }

        private double? getDistanceSnap()
        {
            if (distanceSnapProvider is not ComposerDistanceSnapProvider composerSnapProvider)
                return null;

            // the clock may not land exactly on the start time of an object when seeking to it.
            double time = EditorBeatmap.SelectedHitObjects.Count == 1 ? EditorBeatmap.SelectedHitObjects[0].StartTime : editorClock.CurrentTime + 1;

            HitObject? current = null;
            HitObject? previous = null;

            foreach (var hitObject in EditorBeatmap.HitObjects)
            {
                if (hitObject.StartTime > time)
                    break;

                previous = current;
                current = hitObject;
            }

            if (current == null || previous == null || !hasPosition(previous) || !hasPosition(current))
                return null;

            double snap = composerSnapProvider.ReadCurrentDistanceSnap(previous, current);

            // e.g. stacked objects, for which a distance snap isn't meaningful.
            return snap > 0 && double.IsFinite(snap) ? snap : null;

            // e.g. spinners, which have no position to measure the distance from or to.
            static bool hasPosition(HitObject hitObject) => hitObject is not IHasDuration || hitObject is IHasPath;
        }

        private void updateText()
        {
            InspectorText.Clear();

            var info = difficultyInfo.Value;

            lastDifficultyName = EditorBeatmap.BeatmapInfo.DifficultyName;

            AddHeader("Difficulty");
            AddValue(string.IsNullOrEmpty(lastDifficultyName) ? "-" : lastDifficultyName);

            AddHeader("Max combo");
            AddValue(info == null ? "-" : $"{info.MaxCombo}x");

            AddHeader("Star rating");
            AddValue(info == null ? "-" : $"{info.StarRating:0.00}");

            AddHeader("Timing");
            AddValue($"{lastBPM:0.00} BPM @ {lastTimeSignature}");

            if (distanceSnapProvider is ComposerDistanceSnapProvider)
            {
                AddHeader("Distance snap");
                AddValue(lastDistanceSnap);
            }
        }
    }
}
