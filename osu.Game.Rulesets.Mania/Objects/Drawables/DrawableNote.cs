// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Objects.Drawables
{
    /// <summary>
    /// Visualises a <see cref="Note"/> hit object.
    /// </summary>
    public partial class DrawableNote : DrawableManiaHitObject<Note>, IKeyBindingHandler<ManiaAction>
    {
        [Resolved(canBeNull: true)]
        private IBeatmap beatmap { get; set; }

        private readonly Bindable<bool> configTimingBasedNoteColouring = new Bindable<bool>();

        private static readonly OsuSetting[] snap_colour_settings =
        {
            OsuSetting.SlopManiaSnapColour2,
            OsuSetting.SlopManiaSnapColour3,
            OsuSetting.SlopManiaSnapColour4,
            OsuSetting.SlopManiaSnapColour6,
            OsuSetting.SlopManiaSnapColour8,
            OsuSetting.SlopManiaSnapColour12,
            OsuSetting.SlopManiaSnapColour16,
            OsuSetting.SlopManiaSnapColourOther,
        };

        private OsuConfigManager config;
        private readonly List<Bindable<string>> snapColours = new List<Bindable<string>>();

        protected virtual ManiaSkinComponents Component => ManiaSkinComponents.Note;

        private Drawable headPiece;

        public DrawableNote()
            : this(null)
        {
        }

        public DrawableNote(Note hitObject)
            : base(hitObject)
        {
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader(true)]
        private void load(ManiaRulesetConfigManager rulesetConfig, OsuConfigManager config)
        {
            rulesetConfig?.BindWith(ManiaRulesetSetting.TimingBasedNoteColouring, configTimingBasedNoteColouring);

            this.config = config;

            if (config != null)
            {
                foreach (var setting in snap_colour_settings)
                {
                    var colour = config.GetBindable<string>(setting);
                    colour.BindValueChanged(_ => Scheduler.AddOnce(updateSnapColour));
                    snapColours.Add(colour);
                }
            }

            AddInternal(headPiece = new SkinnableDrawable(new ManiaSkinComponentLookup(Component), _ => new DefaultNotePiece())
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            configTimingBasedNoteColouring.BindValueChanged(_ => updateSnapColour());
            StartTimeBindable.BindValueChanged(_ => updateSnapColour(), true);
        }

        protected override void OnApply()
        {
            base.OnApply();
            updateSnapColour();
        }

        protected override void OnDirectionChanged(ValueChangedEvent<ScrollingDirection> e)
        {
            base.OnDirectionChanged(e);

            headPiece.Anchor = headPiece.Origin = e.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            Debug.Assert(HitObject.HitWindows != null);

            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyMinResult();

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);

            if (result == HitResult.None)
                return;

            result = GetCappedResult(result);
            ApplyResult(result);
        }

        /// <summary>
        /// Some objects in mania may want to limit the max result.
        /// </summary>
        protected virtual HitResult GetCappedResult(HitResult result) => result;

        public virtual bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action != Action.Value)
                return false;

            if (CheckHittable?.Invoke(this, Time.Current) == false)
                return false;

            return UpdateResult(true);
        }

        public virtual void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
        }

        private void updateSnapColour()
        {
            if (beatmap == null || HitObject == null) return;

            int snapDivisor = beatmap.ControlPointInfo.GetClosestBeatDivisor(HitObject.StartTime);

            if (!configTimingBasedNoteColouring.Value || config == null)
            {
                Colour = Colour4.White;
                return;
            }

            // uses the customisable palette of osu!stable instead of the editor's beat snap colours.
            Colour = SlopColours.Parse(config.Get<string>(SlopColours.GetManiaSnapColourSetting(snapDivisor)), Colour4.White);
        }
    }
}
