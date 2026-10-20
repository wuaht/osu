// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// An <see cref="ISkinSource"/> which provides the skin selected via <see cref="OsuSetting.SlopEditorAutoplayCursorSkin"/> for the editor autoplay cursor.
    /// If no skin is selected (or it can no longer be found), this forwards to the parent source, which provides the editor skin.
    /// </summary>
    public class EditorCursorSkinSource : ISkinSource, IDisposable
    {
        public event Action? SourceChanged;

        private readonly ISkinSource parentSource;
        private readonly SkinManager skinManager;
        private readonly IBeatmap beatmap;
        private readonly Bindable<string> cursorSkinSetting;

        private Skin? cursorSkin;
        private ISkin[] cursorSources = Array.Empty<ISkin>();

        public EditorCursorSkinSource(ISkinSource parentSource, SkinManager skinManager, OsuConfigManager config, IBeatmap beatmap)
        {
            this.parentSource = parentSource;
            this.skinManager = skinManager;
            this.beatmap = beatmap;

            cursorSkinSetting = config.GetBindable<string>(OsuSetting.SlopEditorAutoplayCursorSkin);
            cursorSkinSetting.BindValueChanged(_ =>
            {
                updateCursorSkin();
                SourceChanged?.Invoke();
            });

            updateCursorSkin();

            parentSource.SourceChanged += onParentSourceChanged;
        }

        private void updateCursorSkin()
        {
            cursorSkin?.Dispose();
            cursorSkin = null;
            cursorSources = Array.Empty<ISkin>();

            if (!Guid.TryParse(cursorSkinSetting.Value, out var id))
                return;

            cursorSkin = skinManager.Query(s => s.ID == id && !s.DeletePending)?.PerformRead(skinManager.GetSkin);

            if (cursorSkin == null)
                return;

            // legacy skins provide the cursor through the ruleset's skin transformer.
            var ruleset = new OsuRuleset();
            cursorSources = skinManager.GetAllSources(cursorSkin)
                                       .Select(s => s is Skin skin ? ruleset.CreateSkinTransformer(skin, beatmap) ?? s : s)
                                       .ToArray();
        }

        private void onParentSourceChanged() => SourceChanged?.Invoke();

        public IEnumerable<ISkin> AllSources => cursorSkin == null ? parentSource.AllSources : cursorSources;

        public ISkin? FindProvider(Func<ISkin, bool> lookupFunction) => AllSources.FirstOrDefault(lookupFunction);

        public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => lookupWithFallback(s => s.GetDrawableComponent(lookup));

        public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => lookupWithFallback(s => s.GetTexture(componentName, wrapModeS, wrapModeT));

        public ISample? GetSample(ISampleInfo sampleInfo) => lookupWithFallback(s => s.GetSample(sampleInfo));

        public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            where TLookup : notnull
            where TValue : notnull
            => lookupWithFallback(s => s.GetConfig<TLookup, TValue>(lookup));

        private T? lookupWithFallback<T>(Func<ISkin, T?> lookupFunction)
            where T : class
        {
            foreach (var source in AllSources)
            {
                if (lookupFunction(source) is T skinSourced)
                    return skinSourced;
            }

            return null;
        }

        public void Dispose()
        {
            parentSource.SourceChanged -= onParentSourceChanged;
            cursorSkinSetting.UnbindAll();
            cursorSkin?.Dispose();
        }
    }
}
