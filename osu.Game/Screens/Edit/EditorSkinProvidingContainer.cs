// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Skinning;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// A <see cref="SkinProvidingContainer"/> that fires <see cref="ISkinSource.SourceChanged"/> when users have made a change to the beatmap skin
    /// of the map being edited.
    /// </summary>
    public partial class EditorSkinProvidingContainer : RulesetSkinProvidingContainer
    {
        private readonly EditorBeatmapSkin? beatmapSkin;

        private Bindable<bool> alwaysIgnoreBeatmapSamples = null!;

        public EditorSkinProvidingContainer(EditorBeatmap editorBeatmap)
            : base(editorBeatmap.PlayableBeatmap.BeatmapInfo.Ruleset.CreateInstance(), editorBeatmap.PlayableBeatmap, editorBeatmap.BeatmapSkin?.Skin)
        {
            beatmapSkin = editorBeatmap.BeatmapSkin;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            alwaysIgnoreBeatmapSamples = config.GetBindable<bool>(OsuSetting.SlopEditorAlwaysIgnoreBeatmapSamples);
            alwaysIgnoreBeatmapSamples.BindValueChanged(ignore => BeatmapHitsounds.Value = !ignore.NewValue, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (beatmapSkin != null)
                beatmapSkin.BeatmapSkinChanged += triggerSourceChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (beatmapSkin != null)
                beatmapSkin.BeatmapSkinChanged -= triggerSourceChanged;
        }

        private void triggerSourceChanged() => Schedule(TriggerSourceChanged);
    }
}
