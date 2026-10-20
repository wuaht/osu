// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Edit.Blueprints.HitCircles.Components;
using osu.Game.Rulesets.Osu.Edit.Blueprints.Sliders;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit.Streams
{
    /// <summary>
    /// Places an <see cref="EditorStream"/>. The path is drawn exactly like the one of a slider.
    /// </summary>
    public partial class StreamPlacementBlueprint : SliderPlacementBlueprint
    {
        protected override bool ShowsPlacementObject => false;

        protected override bool SnapsLength => false;

        protected override bool ShowsSliderPieces => false;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorStreamManager? streamManager { get; set; }

        [Resolved]
        private OsuSliderVelocityToolboxGroup? sliderVelocityToolbox { get; set; }

        private StreamPathLine pathLine = null!;
        private Container<HitCirclePiece> circlePieces = null!;

        private float circleScale = 1;

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(pathLine = new StreamPathLine { Depth = 2 });
            Add(circlePieces = new Container<HitCirclePiece> { RelativeSizeAxes = Axes.Both, Depth = 1 });

            var template = new HitCircle();
            template.ApplyDefaults(editorBeatmap.ControlPointInfo, editorBeatmap.Difficulty);
            circleScale = template.Scale;
        }

        protected override void OnSliderUpdated()
        {
            base.OnSliderUpdated();

            if (!IsLoaded || PlacementActive != PlacementState.Active)
            {
                pathLine.Alpha = 0;
                circlePieces.Alpha = 0;
                return;
            }

            pathLine.Alpha = 1;
            circlePieces.Alpha = 1;

            pathLine.UpdateFrom(HitObject);

            var layout = createStream().Generate(editorBeatmap.ControlPointInfo, editorBeatmap.Difficulty.SliderMultiplier);

            while (circlePieces.Count < layout.Count)
                circlePieces.Add(new HitCirclePiece());

            for (int i = 0; i < circlePieces.Count; i++)
            {
                var piece = circlePieces[i];

                if (i >= layout.Count)
                {
                    piece.Alpha = 0;
                    continue;
                }

                piece.Alpha = 1;
                piece.Position = layout[i].Position;
                piece.Scale = new Vector2(circleScale);
            }
        }

        protected override void CommitHitObject()
        {
            if (streamManager == null)
            {
                base.CommitHitObject();
                return;
            }

            streamManager.Add(createStream(), HitObject.Samples, HitObject.NewCombo);
        }

        private EditorStream createStream()
        {
            var stream = new EditorStream { BeatDivisor = editorBeatmap.BeatDivisor };

            stream.Path.StartTime = HitObject.StartTime;
            stream.Path.Position = HitObject.Position;
            stream.Path.Path.ControlPoints.AddRange(HitObject.Path.ControlPoints.Select(p => new PathControlPoint(p.Position, p.Type)));

            stream.SetDensity(stream.Path.Path.ControlPoints[0], sliderVelocityToolbox?.SliderVelocity.Value ?? 1);

            return stream;
        }
    }
}
