// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;

namespace osu.Game.Overlays.Visualizer
{
    /// <summary>
    /// A visualizer displayed by the <see cref="VisualizerOverlay"/> on top of the beatmap background.
    /// </summary>
    public abstract partial class VisualizerDrawable : CompositeDrawable
    {
        /// <summary>
        /// The analysed audio of the current track, updated by the <see cref="VisualizerOverlay"/> every frame before children are updated.
        /// </summary>
        protected readonly SpectrumAnalyser Analyser;

        /// <summary>
        /// How much the background is dimmed, in the range [0, 1].
        /// </summary>
        public virtual float BackgroundDim => 0.4f;

        /// <summary>
        /// How much the background zooms in with the bass, relative to its size.
        /// </summary>
        public virtual float BackgroundZoom => 0;

        /// <summary>
        /// How far the background shakes on beats, in pixels.
        /// </summary>
        public virtual float BackgroundShake => 0;

        protected VisualizerDrawable(SpectrumAnalyser analyser)
        {
            Analyser = analyser;
            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Called when the playing track changes.
        /// </summary>
        public virtual void ApplyMetadata(BeatmapMetadata metadata)
        {
        }

        /// <summary>
        /// Called once the background of the playing track has been analysed.
        /// </summary>
        /// <param name="palette">The colours of the background.</param>
        /// <param name="seed">A value which is stable per beatmap set, for deterministic colour choices.</param>
        public virtual void ApplyPalette(BackgroundPalette palette, int seed)
        {
        }
    }
}
