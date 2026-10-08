// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;

namespace osu.Game.Screens.Edit.Compose.Components
{
    /// <summary>
    /// Displays general statistics of the beatmap being edited: its max combo, star rating and the timing at the current time.
    /// </summary>
    public partial class BeatmapStatisticsInspector : EditorInspector
    {
        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private EditorBeatmapDifficulty? beatmapDifficulty { get; set; }

        private readonly IBindable<EditorBeatmapDifficultyInfo?> difficultyInfo = new Bindable<EditorBeatmapDifficultyInfo?>();

        private double lastBPM;
        private TimeSignature? lastTimeSignature;

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

            if (updateTiming())
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

        private void updateText()
        {
            InspectorText.Clear();

            var info = difficultyInfo.Value;

            AddHeader("Max combo");
            AddValue(info == null ? "-" : $"{info.MaxCombo}x");

            AddHeader("Star rating");
            AddValue(info == null ? "-" : $"{info.StarRating:0.00}");

            AddHeader("Timing");
            AddValue($"{lastBPM:0.00} BPM @ {lastTimeSignature}");
        }
    }
}
