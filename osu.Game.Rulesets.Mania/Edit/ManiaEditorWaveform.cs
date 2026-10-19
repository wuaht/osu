// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.UI.Components;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components.Timeline;

namespace osu.Game.Rulesets.Mania.Edit
{
    /// <summary>
    /// Displays the waveform of the track under the columns of a stage, scrolling along with the hit objects.
    /// </summary>
    public partial class ManiaEditorWaveform : HitPositionPaddedContainer
    {
        private const float waveform_alpha = 0.5f;

        private readonly EditorWaveformGraph graph;

        private readonly Bindable<bool> enabled = new Bindable<bool>();
        private readonly Bindable<float> timelineWaveformOpacity = new Bindable<float>();
        private readonly Bindable<EditorWaveformStyle> style = new Bindable<EditorWaveformStyle>();

        private readonly IBindable<double> timeRange = new BindableDouble();

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        private bool trackLoaded;

        public ManiaEditorWaveform()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                Child = graph = new EditorWaveformGraph
                {
                    Origin = Anchor.CentreLeft,
                    Anchor = Anchor.TopCentre,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, IScrollingInfo scrollingInfo)
        {
            config.BindWith(OsuSetting.SlopEditorManiaPlayfieldWaveform, enabled);
            config.BindWith(OsuSetting.EditorWaveformOpacity, timelineWaveformOpacity);
            config.BindWith(OsuSetting.SlopEditorWaveformStyle, style);

            timeRange.BindTo(scrollingInfo.TimeRange);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            enabled.BindValueChanged(_ => updateVisibility());
            timelineWaveformOpacity.BindValueChanged(_ => updateVisibility());
            style.BindValueChanged(s => graph.Style = s.NewValue == EditorWaveformStyle.Default ? EditorWaveformStyle.Simple : s.NewValue, true);
            beatmap.BindValueChanged(_ => trackLoaded = false);

            updateVisibility();
        }

        private void updateVisibility()
        {
            // like the waveform in the timeline, which is replaced by this one, the waveform is hidden by setting its opacity to zero.
            Alpha = enabled.Value && timelineWaveformOpacity.Value > 0 ? waveform_alpha : 0;
        }

        protected override void Update()
        {
            base.Update();

            if (Alpha == 0 || timeRange.Value <= 0)
                return;

            double trackLength = beatmap.Value.Track.Length;

            if (trackLength <= 0)
                return;

            if (!trackLoaded)
            {
                trackLoaded = true;
                graph.LoadTrack(beatmap.Value);
            }

            float scrollLength = DrawHeight - Padding.Top - Padding.Bottom;
            double lengthPerMs = scrollLength / timeRange.Value;

            // the waveform is displayed earlier by the same offset as in the timeline.
            double startPosition = (Time.Current + Editor.WAVEFORM_VISUAL_OFFSET) * lengthPerMs;

            graph.TrackLength = trackLength;
            graph.Width = (float)(trackLength * lengthPerMs);
            graph.Height = DrawWidth;

            if (Direction.Value == ScrollingDirection.Up)
            {
                // time increases downwards from the hit position at the top.
                graph.Rotation = 90;
                graph.Y = (float)-startPosition;
            }
            else
            {
                // time increases upwards from the hit position at the bottom.
                graph.Rotation = -90;
                graph.Y = (float)(scrollLength + startPosition);
            }
        }
    }
}
