// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Containers.BBCode;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osuTK.Graphics;

namespace osu.Game.Overlays.BeatmapSet
{
    public partial class MetadataSectionDescription : Container
    {
        public readonly Bindable<APIBeatmapSet?> BeatmapSet = new Bindable<APIBeatmapSet?>();

        private readonly OsuScrollContainer scroll;

        public MetadataSectionDescription()
        {
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;
            // the loaded description is only added by scheduled callbacks, which don't run while hidden.
            AlwaysPresent = true;

            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Margin = new MarginPadding { Top = 15 },
                    Text = MetadataType.Description.GetLocalisableDescription(),
                    Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 14),
                },
                scroll = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = 34, Bottom = 15 },
                    ScrollbarOverlapsContent = false,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            BeatmapSet.BindValueChanged(s => updateDescription(s.NewValue), true);
        }

        private void updateDescription(APIBeatmapSet? beatmapSet)
        {
            string html = beatmapSet?.Description?.Description ?? string.Empty;

            if (string.IsNullOrWhiteSpace(html))
            {
                this.FadeOut(250);
                scroll.Clear();
                return;
            }

            LoadComponentAsync(new DrawableBBCode(html)
            {
                Colour = Color4.White.Opacity(0.75f),
            }, loaded =>
            {
                if (beatmapSet != BeatmapSet.Value)
                    return;

                scroll.Clear();
                scroll.Add(loaded);
                this.FadeIn(250);
            });
        }
    }
}
