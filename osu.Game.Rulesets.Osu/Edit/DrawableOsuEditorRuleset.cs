// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Osu.UI.Cursor;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Edit;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    public partial class DrawableOsuEditorRuleset : DrawableOsuRuleset
    {
        public DrawableOsuEditorRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod> mods)
            : base(ruleset, beatmap, mods)
        {
        }

        protected override Playfield CreatePlayfield() => new OsuEditorPlayfield();

        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer() => new OsuPlayfieldAdjustmentContainer { Size = Vector2.One };

        private partial class OsuEditorPlayfield : OsuPlayfield
        {
            [Resolved]
            private EditorBeatmap editorBeatmap { get; set; } = null!;

            private Bindable<bool> showAutoplayCursor = null!;

            /// <summary>
            /// The autoplay cursor is slightly transparent by default, so that it does not cover the objects being edited.
            /// </summary>
            private Bindable<float> autoplayCursorOpacity = null!;

            protected override GameplayCursorContainer CreateCursor() => new OsuEditorCursorContainer();

            public OsuEditorPlayfield()
            {
                HitPolicy = new AnyOrderHitPolicy();
            }

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config)
            {
                showAutoplayCursor = config.GetBindable<bool>(OsuSetting.SlopEditorShowAutoplayCursor);
                autoplayCursorOpacity = config.GetBindable<float>(OsuSetting.SlopEditorAutoplayCursorOpacity);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                editorBeatmap.BeatmapReprocessed += onBeatmapReprocessed;

                showAutoplayCursor.BindValueChanged(_ => updateCursorAlpha());
                autoplayCursorOpacity.BindValueChanged(_ => updateCursorAlpha(), true);
            }

            private void updateCursorAlpha() => Cursor!.Alpha = showAutoplayCursor.Value ? autoplayCursorOpacity.Value : 0;

            private void onBeatmapReprocessed() => ApplyCircleSizeToPlayfieldBorder(editorBeatmap);

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                if (editorBeatmap.IsNotNull())
                    editorBeatmap.BeatmapReprocessed -= onBeatmapReprocessed;
            }
        }

        /// <summary>
        /// Displays the cursor of the autoplay replay.
        /// </summary>
        private partial class OsuEditorCursorContainer : OsuCursorContainer
        {
            [Resolved]
            private EditorClock editorClock { get; set; } = null!;

            private EditorCursorSkinSource? cursorSkinSource;

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

                dependencies.CacheAs<ISkinSource>(cursorSkinSource = new EditorCursorSkinSource(
                    parent.Get<ISkinSource>(), parent.Get<SkinManager>(), parent.Get<OsuConfigManager>(), parent.Get<IBeatmap>()));

                return dependencies;
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                cursorSkinSource?.Dispose();
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                // seeking moves the cursor across the playfield instantly, which would leave long trails behind.
                if (editorClock.SeekingOrStopped.Value)
                    ClearTrails();
            }
        }
    }
}
