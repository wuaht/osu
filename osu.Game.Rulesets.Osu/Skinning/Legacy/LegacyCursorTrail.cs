// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Rulesets.Osu.UI.Cursor;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Osu.Skinning.Legacy
{
    public partial class LegacyCursorTrail : CursorTrail
    {
        private readonly ISkin skin;
        private const double disjoint_trail_time_separation = 1000 / 60.0;

        public bool DisjointTrail { get; private set; }
        private double lastTrailTime;

        private IBindable<float> cursorSize = null!;

        private IBindable<CursorTrailStyle> trailStyle = null!;

        private bool skinAllowsRotation;

        /// <summary>
        /// Whether the disjoint trail is replaced by evenly spaced, quickly fading trail parts along the cursor path.
        /// </summary>
        private bool atrias => DisjointTrail && trailStyle.Value == CursorTrailStyle.Atrias;

        private Vector2? currentPosition;

        public LegacyCursorTrail(ISkin skin)
        {
            this.skin = skin;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, ISkinSource skinSource)
        {
            cursorSize = config.GetBindable<float>(OsuSetting.GameplayCursorSize).GetBoundCopy();
            skinAllowsRotation = skin.GetConfig<OsuSkinConfiguration, bool>(OsuSkinConfiguration.CursorTrailRotate)?.Value ?? true;
            trailStyle = config.GetBindable<CursorTrailStyle>(OsuSetting.SlopCursorTrailStyle).GetBoundCopy();

            Texture = skin.GetTexture("cursortrail");

            // Cursor and cursor trail components are sourced from potentially different skin sources.
            // Stable always chooses cursor trail disjoint behaviour based on the cursor texture lookup source, so we need to fetch where that occurred.
            // See https://github.com/peppy/osu-stable-reference/blob/3ea48705eb67172c430371dcfc8a16a002ed0d3d/osu!/Graphics/Skinning/SkinManager.cs#L269
            var cursorProvider = skinSource.FindProvider(s => s.GetTexture("cursor") != null);
            DisjointTrail = cursorProvider?.GetTexture("cursormiddle") == null;

            if (DisjointTrail)
            {
                bool centre = skin.GetConfig<OsuSkinConfiguration, bool>(OsuSkinConfiguration.CursorCentre)?.Value ?? true;

                TrailOrigin = centre ? Anchor.Centre : Anchor.TopLeft;
                Blending = BlendingParameters.Inherit;
            }
            else
            {
                Blending = BlendingParameters.Additive;
            }

            Texture?.ScaleAdjust *= LegacySkin.STABLE_MAGIC_SCALE_FACTOR;

            trailStyle.BindValueChanged(_ => AllowPartRotation = skinAllowsRotation && !atrias, true);
        }

        protected override double FadeDuration => atrias ? 60 : DisjointTrail ? 150 : 500;
        protected override float FadeExponent => 1;

        protected override bool InterpolateMovements => !DisjointTrail || atrias;

        // Atrias trail parts are spaced a tenth of the (unscaled) trail texture apart, regardless of cursor size.
        protected override float IntervalMultiplier => atrias ? 0.4f / Math.Max(CursorScale.X, 0.01f) : 1 / Math.Max(cursorSize.Value, 1);
        protected override bool AvoidDrawingNearCursor => !DisjointTrail;
        protected override bool AllowSmootherTrail => !DisjointTrail;

        protected override void Update()
        {
            base.Update();

            if (!DisjointTrail || atrias || !currentPosition.HasValue)
                return;

            if (Time.Current - lastTrailTime >= disjoint_trail_time_separation)
            {
                lastTrailTime = Time.Current;
                AddTrail(currentPosition.Value);
            }
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (!DisjointTrail || atrias)
                return base.OnMouseMove(e);

            currentPosition = e.ScreenSpaceMousePosition;

            // Intentionally block the base call as we're adding the trails ourselves.
            return false;
        }
    }
}
