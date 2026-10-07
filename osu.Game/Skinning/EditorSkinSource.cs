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
using osu.Game.Configuration;

namespace osu.Game.Skinning
{
    /// <summary>
    /// An <see cref="ISkinSource"/> which provides the skin selected via <see cref="OsuSetting.SlopEditorSkin"/>
    /// in place of the user's regular gameplay skin.
    /// If no editor skin is selected (or it can no longer be found), this transparently forwards to the <see cref="SkinManager"/>.
    /// </summary>
    /// <remarks>
    /// Intended to be cached as <see cref="ISkinSource"/> for the editor's dependency tree (and optionally for editor test play),
    /// so that the regular skin remains in use everywhere else.
    /// </remarks>
    public class EditorSkinSource : ISkinSource, IDisposable
    {
        public event Action? SourceChanged;

        private readonly SkinManager skinManager;
        private readonly Bindable<string> editorSkinSetting;

        /// <summary>
        /// The skin to use in place of the gameplay skin, or <c>null</c> to use the gameplay skin.
        /// </summary>
        private Skin? editorSkin;

        public EditorSkinSource(SkinManager skinManager, OsuConfigManager config)
        {
            this.skinManager = skinManager;

            editorSkinSetting = config.GetBindable<string>(OsuSetting.SlopEditorSkin);
            editorSkinSetting.BindValueChanged(_ =>
            {
                updateEditorSkin();
                SourceChanged?.Invoke();
            });

            updateEditorSkin();

            skinManager.SourceChanged += onSkinManagerSourceChanged;
        }

        private void updateEditorSkin()
        {
            editorSkin = null;

            if (!Guid.TryParse(editorSkinSetting.Value, out var id))
                return;

            editorSkin = skinManager.Query(s => s.ID == id && !s.DeletePending)?.PerformRead(skinManager.GetSkin);
        }

        private void onSkinManagerSourceChanged() => SourceChanged?.Invoke();

        public IEnumerable<ISkin> AllSources
        {
            get
            {
                if (editorSkin == null)
                {
                    foreach (var source in skinManager.AllSources)
                        yield return source;

                    yield break;
                }

                yield return editorSkin;

                // Mirror the fallbacks provided by SkinManager, which handle cases where a skin doesn't
                // have the required resources for complete display of certain elements.
                if (editorSkin is LegacySkin && editorSkin != skinManager.DefaultClassicSkin)
                    yield return skinManager.DefaultClassicSkin;

                if (editorSkin is not TrianglesSkin)
                {
                    // The triangles skin is always part of the skin manager's sources (either as the current skin or as a fallback).
                    var trianglesSkin = skinManager.AllSources.OfType<TrianglesSkin>().LastOrDefault();

                    if (trianglesSkin != null)
                        yield return trianglesSkin;
                }
            }
        }

        public ISkin? FindProvider(Func<ISkin, bool> lookupFunction)
        {
            foreach (var source in AllSources)
            {
                if (lookupFunction(source))
                    return source;
            }

            return null;
        }

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
            skinManager.SourceChanged -= onSkinManagerSourceChanged;
            editorSkinSetting.UnbindAll();
        }
    }
}
