// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.UI;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A type of <see cref="SkinProvidingContainer"/> specialized for <see cref="DrawableRuleset"/> and other gameplay-related components.
    /// Providing access to parent skin sources and the beatmap skin each surrounded with the ruleset legacy skin transformer.
    /// </summary>
    public partial class RulesetSkinProvidingContainer : SkinProvidingContainer
    {
        public BindableWithCurrent<bool> BeatmapSkins = new BindableWithCurrent<bool>(true);
        public BindableWithCurrent<bool> BeatmapColours = new BindableWithCurrent<bool>(true);
        public BindableWithCurrent<bool> BeatmapHitsounds = new BindableWithCurrent<bool>(true);

        protected readonly Ruleset Ruleset;
        protected readonly IBeatmap Beatmap;

        [CanBeNull]
        private readonly ISkin beatmapSkin;

        /// <remarks>
        /// This container already re-exposes all parent <see cref="ISkinSource"/> sources in a ruleset-usable form.
        /// Therefore disallow falling back to any parent <see cref="ISkinSource"/> any further.
        /// </remarks>
        protected override bool AllowFallingBackToParent => false;

        /// <summary>
        /// Whether the skin chosen for this ruleset in the settings should be used in place of the main skin.
        /// </summary>
        public bool UseRulesetSkinPreference { get; init; }

        [CanBeNull]
        private Skin rulesetSkin;

        protected override Container<Drawable> Content { get; } = new Container
        {
            RelativeSizeAxes = Axes.Both,
        };

        public RulesetSkinProvidingContainer(Ruleset ruleset, IBeatmap beatmap, [CanBeNull] ISkin beatmapSkin)
        {
            Ruleset = ruleset;
            Beatmap = beatmap;
            this.beatmapSkin = beatmapSkin;
        }

        [BackgroundDependencyLoader]
        private void load(SkinManager skinManager)
        {
            InternalChild = new BeatmapSkinProvidingContainer(GetRulesetTransformedSkin(beatmapSkin), GetRulesetTransformedSkin(skinManager.DefaultClassicSkin))
            {
                Child = Content,
                BeatmapSkins = BeatmapSkins,
                BeatmapColours = BeatmapColours,
                BeatmapHitsounds = BeatmapHitsounds,
            };
        }

        private ResourceStoreBackedSkin rulesetResourcesSkin;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            if (Ruleset.CreateResourceStore() is IResourceStore<byte[]> resources)
                rulesetResourcesSkin = new ResourceStoreBackedSkin(resources, parent.Get<GameHost>(), parent.Get<AudioManager>());

            if (UseRulesetSkinPreference)
                rulesetSkin = getRulesetSkin(parent.Get<SkinManager>(), parent.Get<OsuConfigManager>());

            return base.CreateChildDependencies(parent);
        }

        protected override void RefreshSources()
        {
            // Populate a local list first so we can adjust the returned order as we go.
            var sources = new List<ISkin>();

            Debug.Assert(ParentSource != null);

            var parentSources = rulesetSkin != null && ParentSource is SkinManager skinManager
                ? skinManager.GetAllSources(rulesetSkin)
                : ParentSource.AllSources;

            foreach (var source in parentSources)
            {
                switch (source)
                {
                    case Skin skin:
                        sources.Add(GetRulesetTransformedSkin(skin));
                        break;

                    default:
                        sources.Add(source);
                        break;
                }
            }

            // TODO: check
            int lastDefaultSkinIndex = sources.IndexOf(sources.OfType<TrianglesSkin>().LastOrDefault());

            // Ruleset resources should be given the ability to override game-wide defaults
            // This is achieved by placing them before the last instance of DefaultSkin.
            // Note that DefaultSkin may not be present in some test scenes.
            if (lastDefaultSkinIndex >= 0)
                sources.Insert(lastDefaultSkinIndex, rulesetResourcesSkin);
            else
                sources.Add(rulesetResourcesSkin);

            SetSources(sources);
        }

        [CanBeNull]
        private Skin getRulesetSkin([CanBeNull] SkinManager skinManager, [CanBeNull] OsuConfigManager config)
        {
            if (skinManager == null || config == null)
                return null;

            OsuSetting? setting = Ruleset.ShortName switch
            {
                @"taiko" => OsuSetting.SlopTaikoSkin,
                @"fruits" => OsuSetting.SlopCatchSkin,
                @"mania" => OsuSetting.SlopManiaSkin,
                _ => null
            };

            if (setting == null || !Guid.TryParse(config.Get<string>(setting.Value), out Guid id))
                return null;

            // the main skin is already provided by the parent.
            if (id == skinManager.CurrentSkinInfo.Value.ID)
                return null;

            return skinManager.Query(s => s.ID == id && !s.DeletePending)?.PerformRead(skinManager.GetSkin);
        }

        protected ISkin GetRulesetTransformedSkin(ISkin skin)
        {
            if (skin == null)
                return null;

            var rulesetTransformed = Ruleset.CreateSkinTransformer(skin, Beatmap);
            if (rulesetTransformed != null)
                return rulesetTransformed;

            return skin;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            rulesetResourcesSkin?.Dispose();
            rulesetSkin?.Dispose();
        }
    }
}
