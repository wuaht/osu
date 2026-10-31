// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Caching;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Edit
{
    /// <summary>
    /// A wrapper for a <see cref="DrawableRuleset{TObject}"/>. Handles adding visual representations of <see cref="HitObject"/>s to the underlying <see cref="DrawableRuleset{TObject}"/>.
    /// </summary>
    internal partial class DrawableEditorRulesetWrapper<TObject> : CompositeDrawable
        where TObject : HitObject
    {
        public Playfield Playfield => drawableRuleset.Playfield;

        private readonly DrawableRuleset<TObject> drawableRuleset;

        [Resolved]
        private EditorBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        public DrawableEditorRulesetWrapper(DrawableRuleset<TObject> drawableRuleset)
        {
            this.drawableRuleset = drawableRuleset;

            RelativeSizeAxes = Axes.Both;

            InternalChild = drawableRuleset;
        }

        private Bindable<SlopAutoplayEngine> autoplayEngine = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            drawableRuleset.FrameStablePlayback = false;
            Playfield.DisplayJudgements.Value = false;

            autoplayEngine = config.GetBindable<SlopAutoplayEngine>(OsuSetting.SlopEditorAutoplayEngine);
        }

        [Resolved]
        private IEditorChangeHandler? changeHandler { get; set; }

        private readonly Cached autoplayRegenerated = new Cached();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatmap.HitObjectAdded += hitObjectAdded;
            beatmap.HitObjectRemoved += hitObjectRemoved;

            if (changeHandler != null)
            {
                // for now only regenerate replay on a finalised state change, not HitObjectUpdated.
                changeHandler.OnStateChange += stateChanged;
            }
            else
            {
                beatmap.HitObjectUpdated += hitObjectUpdated;
            }

            autoplayEngine.BindValueChanged(_ => Scheduler.AddOnce(regenerateAutoplay));

            Scheduler.AddOnce(regenerateAutoplay);
        }

        protected override void Update()
        {
            base.Update();

            // Frame-stable playback is enabled in selected circumstances to ensure that the autoplay replay
            // plays all the important frames that are required for the gameplay preview inside editor to look and sound correctly
            // (hit animations present, hitsounds play in time).
            // Of note:
            // - Frame stability is only active when the editor clock is actually running.
            //   If it were to be enabled while the editor clock was not running, instant seeks could become non-instant.
            //   Moreover, the impact of it being off in that scenario is reduced as hitsounds do not play when the editor clock is paused anyway.
            // - Autoplay regenerations turn off frame stability for a frame.
            //   This is because substituting the autoplay replay under the drawable ruleset while the editor clock is running
            //   would result in the drawable ruleset replaying the entirety of the new replay from time 0 until the editor clock's current time
            //   which results in long frame times and many hitsounds playing at once.
            // - Large seeks turn off frame stability as they would also provoke a clock catch-up,
            //   which will result in long frame times and many hitsounds playing at once.
            bool inLargeSeek = Math.Abs(drawableRuleset.FrameStableClock.CurrentTime - editorClock.CurrentTime) > 1000;
            drawableRuleset.FrameStablePlayback = editorClock.IsRunning && autoplayRegenerated.IsValid && !inLargeSeek;
            autoplayRegenerated.Validate();
        }

        private CancellationTokenSource? autoplayCancellation;

        private void regenerateAutoplay()
        {
            autoplayCancellation?.Cancel();

            var autoplayMod = drawableRuleset.Mods.OfType<ModAutoplay>().Single();
            var playableBeatmap = drawableRuleset.Beatmap;
            var mods = drawableRuleset.Mods;

            // the first replay is needed right away for the playfield to show hit objects as hit.
            if (autoplayCancellation == null)
            {
                autoplayCancellation = new CancellationTokenSource();
                applyAutoplay(autoplayMod.CreateScoreFromReplayData(playableBeatmap, mods));
                return;
            }

            var source = autoplayCancellation = new CancellationTokenSource();

            // generating takes long for big beatmaps, so it shouldn't block the update thread after every change.
            // the beatmap may change while generating, in which case a newer generation cancels this one.
            Task.Run(() => autoplayMod.CreateScoreFromReplayData(playableBeatmap, mods), source.Token).ContinueWith(task =>
            {
                if (source.IsCancellationRequested)
                    return;

                if (task.IsFaulted)
                {
                    Logger.Log($"Failed to generate the autoplay replay in the editor: {task.Exception}", level: LogLevel.Debug);
                    return;
                }

                var score = task.GetResultSafely();

                Schedule(() =>
                {
                    if (!source.IsCancellationRequested)
                        applyAutoplay(score);
                });
            }, CancellationToken.None);
        }

        private void applyAutoplay(Score score)
        {
            drawableRuleset.SetReplayScore(score);
            autoplayRegenerated.Invalidate();
        }

        private void hitObjectAdded(HitObject hitObject)
        {
            drawableRuleset.AddHitObject((TObject)hitObject);
            drawableRuleset.Playfield.PostProcess();
        }

        private void hitObjectRemoved(HitObject hitObject)
        {
            drawableRuleset.RemoveHitObject((TObject)hitObject);
            drawableRuleset.Playfield.PostProcess();
        }

        private void hitObjectUpdated(HitObject _) => Scheduler.AddOnce(regenerateAutoplay);

        private void stateChanged() => Scheduler.AddOnce(regenerateAutoplay);

        public override bool PropagatePositionalInputSubTree => false;

        public override bool PropagateNonPositionalInputSubTree => false;

        public PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer() => drawableRuleset.CreatePlayfieldAdjustmentContainer();

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            autoplayCancellation?.Cancel();

            if (beatmap.IsNotNull())
            {
                beatmap.HitObjectAdded -= hitObjectAdded;
                beatmap.HitObjectRemoved -= hitObjectRemoved;
                beatmap.HitObjectUpdated -= hitObjectUpdated;
            }

            if (changeHandler != null)
                changeHandler.OnStateChange -= stateChanged;
        }
    }
}
