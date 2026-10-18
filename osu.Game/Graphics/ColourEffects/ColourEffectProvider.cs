// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;

namespace osu.Game.Graphics.ColourEffects
{
    /// <summary>
    /// Drives all rainbow / gradient colour effects from a single shared phase, so that all effects stay in sync.
    /// </summary>
    public partial class ColourEffectProvider : Component
    {
        /// <summary>
        /// The current shared cycle phase, in the range [0, 1).
        /// </summary>
        public float Phase { get; private set; }

        private readonly Dictionary<ColourEffectTarget, TargetState> targets = new Dictionary<ColourEffectTarget, TargetState>();

        private Bindable<float> speed = null!;
        private Bindable<float> rainbowSaturation = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            speed = config.GetBindable<float>(OsuSetting.SlopColourEffectSpeed);
            rainbowSaturation = config.GetBindable<float>(OsuSetting.SlopColourEffectRainbowSaturation);

            targets[ColourEffectTarget.Cursor] = new TargetState(config, OsuSetting.SlopCursorColourEffect, OsuSetting.SlopCursorColourEffectMode, OsuSetting.SlopCursorColourEffectGradient);
            targets[ColourEffectTarget.CursorTrail] = new TargetState(config, OsuSetting.SlopCursorTrailColourEffect, OsuSetting.SlopCursorTrailColourEffectMode, OsuSetting.SlopCursorTrailColourEffectGradient);
            targets[ColourEffectTarget.ComboColours] = new TargetState(config, OsuSetting.SlopComboColourEffect, OsuSetting.SlopComboColourEffectMode, OsuSetting.SlopComboColourEffectGradient);
        }

        protected override void Update()
        {
            base.Update();

            // accumulate instead of deriving from absolute time, so that speed changes don't make the colours jump.
            double delta = Math.Clamp(Clock.ElapsedFrameTime, 0, 1000);
            Phase = (float)((Phase + delta * speed.Value / ColourEffect.CYCLE_DURATION) % 1);
        }

        /// <summary>
        /// Whether the effect of the given target is enabled.
        /// </summary>
        public bool IsEnabled(ColourEffectTarget target) => targets[target].Enabled.Value;

        /// <summary>
        /// Gets the current colour of the given target, regardless of whether its effect is enabled.
        /// </summary>
        public Colour4 GetColour(ColourEffectTarget target)
        {
            var state = targets[target];
            return ColourEffect.GetColour(state.Mode.Value, state.Stops, Phase, rainbowSaturation.Value);
        }

        /// <summary>
        /// Gets the current colour of the given target if its effect is enabled.
        /// </summary>
        public bool TryGetColour(ColourEffectTarget target, out Colour4 colour)
        {
            if (!IsEnabled(target))
            {
                colour = Colour4.White;
                return false;
            }

            colour = GetColour(target);
            return true;
        }

        private class TargetState
        {
            public readonly Bindable<bool> Enabled;
            public readonly Bindable<ColourEffectMode> Mode;

            public IReadOnlyList<Colour4> Stops { get; private set; } = Array.Empty<Colour4>();

            private readonly Bindable<string> gradient;

            public TargetState(OsuConfigManager config, OsuSetting enabled, OsuSetting mode, OsuSetting gradientSetting)
            {
                Enabled = config.GetBindable<bool>(enabled);
                Mode = config.GetBindable<ColourEffectMode>(mode);
                gradient = config.GetBindable<string>(gradientSetting);
                gradient.BindValueChanged(g => Stops = ColourEffect.ParseGradient(g.NewValue), true);
            }
        }
    }
}
