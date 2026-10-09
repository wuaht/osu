// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Fposu.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.Fposu.UI
{
    /// <summary>
    /// The osu! gameplay, displayed in first person.
    /// </summary>
    public partial class DrawableFposuRuleset : DrawableOsuRuleset
    {
        // initialised before the base constructor, which creates the input manager and the playfield adjustment container.
        private readonly FposuCamera camera = new FposuCamera();

        /// <summary>
        /// The camera which the game is seen through.
        /// </summary>
        public FposuCamera Camera => camera;

        /// <summary>
        /// The screen which the game is displayed on.
        /// </summary>
        public FposuScreen Screen => mainAdjustmentContainer!.Screen;

        /// <summary>
        /// The input manager which turns the camera.
        /// </summary>
        public FposuInputManager FposuInputManager => (FposuInputManager)base.KeyBindingInputManager;

        private FposuPlayfieldAdjustmentContainer? mainAdjustmentContainer;
        private bool mainInputManagerCreated;

        public DrawableFposuRuleset(FposuRuleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
            // overlays of mods (e.g. flashlight and blinds) belong to the playfield, so they are displayed on the screen as well.
            mainAdjustmentContainer!.RulesetOverlays.Add(Overlays.CreateProxy());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var depthMod = Mods.OfType<FposuModDepth>().FirstOrDefault();

            if (depthMod != null)
                mainAdjustmentContainer!.DepthLayer.Apply(depthMod, Playfield);
        }

        // the input manager and the playfield adjustment container are also created for the resume overlay, which shouldn't be displayed in 3D.
        // the key bindings of osu! are used, as the gameplay is the same.
        protected override PassThroughInputManager CreateInputManager()
        {
            var osuRulesetInfo = ((FposuRuleset)Ruleset).OsuRulesetInfo;

            if (mainInputManagerCreated)
                return new ResumeOverlayInputManager(osuRulesetInfo);

            mainInputManagerCreated = true;
            return new FposuInputManager(osuRulesetInfo, camera, () => mainAdjustmentContainer?.Screen);
        }

        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer()
        {
            if (mainAdjustmentContainer != null)
                return new OsuPlayfieldAdjustmentContainer { AlignWithStoryboard = true };

            return mainAdjustmentContainer = new FposuPlayfieldAdjustmentContainer(camera);
        }

        // resuming by clicking the cursor (osu!'s default) doesn't work well while turning the camera, so a countdown is used instead.
        protected override ResumeOverlay CreateResumeOverlay() => new DelayedResumeOverlay { Scale = new Vector2(0.65f) };

        // displayed on the screen like the playfield, where the skip button can be clicked by looking at it.
        public override Container? BreakAndSkipOverlayContainer => mainAdjustmentContainer?.BreakAndSkipOverlays;

        /// <summary>
        /// The input manager of the resume overlay, which is a countdown and so doesn't need positional input.
        /// It's displayed above the game, so it would otherwise block clicks on the screen (e.g. on the skip button) while mouse buttons are disabled.
        /// </summary>
        private partial class ResumeOverlayInputManager : OsuInputManager
        {
            public ResumeOverlayInputManager(RulesetInfo ruleset)
                : base(ruleset)
            {
            }

            public override bool PropagatePositionalInputSubTree => false;
        }

        // the cursor is part of the 2D game displayed in 3D, so it mustn't be proxied above the HUD (which would draw it at its 2D position).
        public override GameplayCursorContainer? Cursor => null;
    }
}
