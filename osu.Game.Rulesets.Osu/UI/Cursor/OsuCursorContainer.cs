// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Graphics.ColourEffects;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Skinning.Argon;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI.Cursor
{
    public partial class OsuCursorContainer : GameplayCursorContainer, IKeyBindingHandler<OsuAction>
    {
        public new OsuCursor ActiveCursor => (OsuCursor)base.ActiveCursor;

        protected override Drawable CreateCursor() => new OsuCursor();
        protected override Container<Drawable> Content => fadeContainer;

        private readonly Container<Drawable> fadeContainer;

        private readonly Bindable<bool> showTrail = new Bindable<bool>(true);

        private readonly Bindable<bool> forceArgonTrail = new Bindable<bool>();

        private readonly Container trailContainer;

        private readonly SkinnableDrawable cursorTrail;

        /// <summary>
        /// The argon cursor trail, which replaces the trail of the skin if <see cref="OsuSetting.SlopForceArgonCursorTrail"/> is enabled.
        /// Wrapped in a container, as <see cref="CursorTrail"/> is always present and would otherwise keep drawing while hidden.
        /// </summary>
        private readonly Container argonCursorTrailContainer;

        private readonly ArgonCursorTrail argonCursorTrail;

        /// <summary>
        /// The trail which is currently displayed.
        /// </summary>
        private CursorTrail activeTrail => forceArgonTrail.Value ? argonCursorTrail : cursorTrail.Drawable as CursorTrail;

        private readonly CursorRippleVisualiser rippleVisualiser;

        [Resolved(CanBeNull = true)]
        private ColourEffectProvider colourEffectProvider { get; set; }

        public OsuCursorContainer()
        {
            InternalChild = fadeContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new CompositeDrawable[]
                {
                    trailContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            cursorTrail = new SkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.CursorTrail), _ => new DefaultCursorTrail(), confineMode: ConfineMode.NoScaling),
                            argonCursorTrailContainer = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0,
                                Child = argonCursorTrail = new ArgonCursorTrail(),
                            },
                        }
                    },
                    rippleVisualiser = new CursorRippleVisualiser(),
                    new SkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.CursorParticles), confineMode: ConfineMode.NoScaling),
                }
            };
        }

        [Resolved(CanBeNull = true)]
        private ISkinSource skinSource { get; set; }

        [Resolved(CanBeNull = true)]
        private GameplayState gameplayState { get; set; }

        [Resolved(CanBeNull = true)]
        private IGameplayClock gameplayClock { get; set; }

        /// <summary>
        /// The combo colours of the beatmap over time, used by the <see cref="ColourEffectMode.ComboColours"/> effect mode.
        /// </summary>
        private ColourEffect.ComboColourTimeline comboColourTimeline = ColourEffect.ComboColourTimeline.EMPTY;

        [BackgroundDependencyLoader(true)]
        private void load(OsuRulesetConfigManager rulesetConfig, OsuConfigManager config)
        {
            config?.BindWith(OsuSetting.SlopForceArgonCursorTrail, forceArgonTrail);

            if (skinSource != null)
            {
                skinSource.SourceChanged += updateComboColourTimeline;
                updateComboColourTimeline();
            }

            rulesetConfig?.BindWith(OsuRulesetSetting.ShowCursorTrail, showTrail);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            showTrail.BindValueChanged(v => trailContainer.FadeTo(v.NewValue ? 1 : 0, 200), true);

            forceArgonTrail.BindValueChanged(v =>
            {
                cursorTrail.Alpha = v.NewValue ? 0 : 1;
                argonCursorTrailContainer.Alpha = v.NewValue ? 1 : 0;
            }, true);

            ActiveCursor.CursorScale.BindValueChanged(e =>
            {
                var newScale = new Vector2(e.NewValue);

                rippleVisualiser.CursorScale = newScale;
                updateTrailScale();
            }, true);
            cursorTrail.OnSkinChanged += updateTrailScale;
        }

        private void updateTrailScale()
        {
            var scale = new Vector2(ActiveCursor.CursorScale.Value);

            if (cursorTrail.Drawable is CursorTrail trail) trail.CursorScale = scale;
            argonCursorTrail.CursorScale = scale;
        }

        private int downCount;

        private void updateExpandedState()
        {
            if (downCount > 0)
                ActiveCursor.Expand();
            else
                ActiveCursor.Contract();
        }

        protected override void Update()
        {
            base.Update();

            if (activeTrail is CursorTrail trail)
            {
                trail.NewPartScale = ActiveCursor.CurrentExpandedScale;
                trail.PartRotation = ActiveCursor.CurrentRotation;
            }

            updateColourEffects();
        }

        private void updateComboColourTimeline() => comboColourTimeline = ColourEffect.GetComboColourTimeline(skinSource, gameplayState?.Beatmap);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skinSource != null)
                skinSource.SourceChanged -= updateComboColourTimeline;
        }

        private bool cursorColourEffectApplied;
        private bool trailColourEffectApplied;

        private void updateColourEffects()
        {
            if (colourEffectProvider == null)
                return;

            if (tryGetColour(ColourEffectTarget.Cursor, out var cursorColour))
            {
                ActiveCursor.ApplyColourEffect(cursorColour);
                cursorColourEffectApplied = true;
            }
            else if (cursorColourEffectApplied)
            {
                ActiveCursor.ApplyColourEffect(null);
                cursorColourEffectApplied = false;
            }

            applyColourEffect(trailContainer, ColourEffectTarget.CursorTrail, ref trailColourEffectApplied);

            // skin textures may have their own colour, which would otherwise mix with the effect colour.
            // applied every frame, as the skin trail is recreated on skin changes.
            if (cursorTrail.Drawable is CursorTrail skinTrail)
                skinTrail.Monochrome = trailColourEffectApplied;
            argonCursorTrail.Monochrome = trailColourEffectApplied;
        }

        private void applyColourEffect(Drawable drawable, ColourEffectTarget target, ref bool applied)
        {
            if (tryGetColour(target, out var colour))
            {
                drawable.Colour = colour;
                applied = true;
            }
            else if (applied)
            {
                drawable.Colour = Colour4.White;
                applied = false;
            }
        }

        private bool tryGetColour(ColourEffectTarget target, out Colour4 colour)
        {
            if (colourEffectProvider.GetMode(target) != ColourEffectMode.ComboColours)
                return colourEffectProvider.TryGetColour(target, out colour);

            colour = Colour4.White;

            // follow the combo colours of the beatmap, outside of gameplay keep the default colour.
            if (!colourEffectProvider.IsEnabled(target) || gameplayClock == null || comboColourTimeline.IsEmpty)
                return false;

            colour = comboColourTimeline.GetColourAt(gameplayClock.CurrentTime);
            return true;
        }

        public bool OnPressed(KeyBindingPressEvent<OsuAction> e)
        {
            switch (e.Action)
            {
                case OsuAction.LeftButton:
                case OsuAction.RightButton:
                    downCount++;
                    updateExpandedState();
                    break;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<OsuAction> e)
        {
            switch (e.Action)
            {
                case OsuAction.LeftButton:
                case OsuAction.RightButton:
                    // Todo: Math.Max() is required as a temporary measure to address https://github.com/ppy/osu-framework/issues/2576
                    downCount = Math.Max(0, downCount - 1);

                    if (downCount == 0)
                        updateExpandedState();
                    break;
            }
        }

        public override bool HandlePositionalInput => true; // OverlayContainer will set this false when we go hidden, but we always want to receive input.

        protected override void PopIn()
        {
            fadeContainer.FadeTo(1, 300, Easing.OutQuint);
            ActiveCursor.ScaleTo(1f, 400, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            fadeContainer.FadeTo(0.05f, 450, Easing.OutQuint);
            ActiveCursor.ScaleTo(0.8f, 450, Easing.OutQuint);
        }

        private partial class DefaultCursorTrail : CursorTrail
        {
            [BackgroundDependencyLoader]
            private void load(TextureStore textures)
            {
                Texture = textures.Get(@"Cursor/cursortrail");
                Scale = new Vector2(1 / Texture.ScaleAdjust);
            }
        }
    }
}
