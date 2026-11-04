// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Base for stable's song select elements, which re-read their textures when the skin changes.
    /// </summary>
    public abstract partial class LegacySongSelectComponent : CompositeDrawable, ISerialisableDrawable
    {
        /// <summary>
        /// Stable lays out song select in a 640x480 space.
        /// </summary>
        public const float STABLE_SCALE = 1.6f;

        public bool UsesFixedAnchor { get; set; }

        [Resolved]
        protected ISkinSource Skin { get; private set; } = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Skin.SourceChanged += onSkinChanged;
            UpdateArtwork();
        }

        private void onSkinChanged() => Schedule(UpdateArtwork);

        protected abstract void UpdateArtwork();

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (Skin.IsNotNull())
                Skin.SourceChanged -= onSkinChanged;
        }
    }

    /// <summary>
    /// stable's <c>songselect-top</c>. Like stable, windows wider than 1366 pixels get raw texture column 1365
    /// stretched from window pixel 1365 to the right edge, covering the rest of the texture.
    /// </summary>
    public partial class LegacySongSelectTop : LegacySongSelectComponent
    {
        private const int fill_start = 1365;

        private Container? fill;

        [Resolved]
        private SkinManager? skinManager { get; set; }

        public LegacySongSelectTop()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        // like stable, the bar is solid: clicks on it must not reach the carousel below.
        protected override bool OnHover(HoverEvent e) => true;

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnClick(ClickEvent e) => true;

        protected override void UpdateArtwork()
        {
            ClearInternal();
            fill = null;

            // stable darkens the search text with its default menu-button-background, below the top artwork.
            AddInternal(new Sprite
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-14, 46) * STABLE_SCALE,
                Texture = skinManager?.DefaultClassicSkin.GetTexture("menu-button-background"),
                Colour = Color4.Black.Opacity(102 / 255f),
            });

            if (Skin.GetTexture("songselect-top") is not Texture top)
            {
                // lazer doesn't ship stable's default artwork, so draw its shape instead, measured against stable at 1080p.
                AddRangeInternal(new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = fallback_band_height * STABLE_SCALE,
                        Colour = fallback_colour,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Y = fallback_band_height * STABLE_SCALE,
                        Height = fallback_edge_width * STABLE_SCALE,
                        Colour = fallback_edge_colour,
                    },
                    new Sprite
                    {
                        Texture = fallbackTexture ??= createFallbackTexture(),
                        Size = new Vector2(fallback_width, fallback_tab_height + fallback_edge_width) * STABLE_SCALE,
                    },
                });
                return;
            }

            var fillColumn = top.Crop(new RectangleF(Math.Min(fill_start, top.Width - 1), 0, 1, top.Height));
            fillColumn.ScaleAdjust = top.ScaleAdjust;

            AddRangeInternal(new Drawable[]
            {
                new Sprite { Texture = top },
                fill = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = top.DisplayHeight,
                    Child = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = fillColumn,
                    }
                },
            });
        }

        private const float fallback_band_height = 52.4f;
        private const float fallback_tab_height = 91;
        private const float fallback_curve_start = 249;
        private const float fallback_curve_end = 369;
        private const float fallback_width = 380;
        private const float fallback_edge_width = 0.9f;
        private const int fallback_resolution = 3;

        private static readonly Color4 fallback_colour = new Color4(4, 20, 24, 255);
        private static readonly Color4 fallback_edge_colour = new Color4(36, 93, 81, 255);

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private Texture? fallbackTexture;

        /// <summary>
        /// The left part of stable's default top, which curves down to make room for the leaderboard scope.
        /// </summary>
        private Texture createFallbackTexture()
        {
            int width = (int)(fallback_width * fallback_resolution);
            int height = (int)MathF.Ceiling((fallback_tab_height + fallback_edge_width) * fallback_resolution);
            var image = new SixLabors.ImageSharp.Image<Rgba32>(width, height);

            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / fallback_resolution;
                float t = Math.Clamp((u - fallback_curve_start) / (fallback_curve_end - fallback_curve_start), 0, 1);
                float edge = fallback_tab_height - (fallback_tab_height - fallback_band_height) * t * t * (3 - 2 * t);

                for (int y = 0; y < height; y++)
                {
                    float distance = (edge + fallback_edge_width - (y + 0.5f) / fallback_resolution) * fallback_resolution;
                    float alpha = Math.Clamp(distance + 0.5f, 0, 1);
                    float edgeAmount = Math.Clamp(fallback_edge_width * fallback_resolution - distance + 0.5f, 0, 1);

                    var colour = Interpolation.ValueAt(edgeAmount, fallback_colour, fallback_edge_colour, 0, 1);
                    image[x, y] = new Rgba32(colour.R, colour.G, colour.B, alpha);
                }
            }

            var texture = renderer.CreateTexture(width, height);
            texture.SetData(new TextureUpload(image));
            return texture;
        }

        protected override void Update()
        {
            base.Update();

            if (fill == null)
                return;

            // stable measures this in window pixels, not in its scaled coordinates.
            fill.Alpha = ScreenSpaceDrawQuad.Width > fill_start + 1 ? 1 : 0;
            fill.Padding = new MarginPadding { Left = ToLocalSpace(ScreenSpaceDrawQuad.TopLeft + new Vector2(fill_start, 0)).X };
        }
    }

    /// <summary>
    /// stable's <c>songselect-bottom</c>, stretched across the screen.
    /// </summary>
    public partial class LegacySongSelectBottom : LegacySongSelectComponent
    {
        public LegacySongSelectBottom()
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        // like stable, the bar is solid: clicks on it must not reach the carousel below.
        protected override bool OnHover(HoverEvent e) => true;

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnClick(ClickEvent e) => true;

        protected override void UpdateArtwork()
        {
            ClearInternal();

            Texture? bottom = Skin.GetTexture("songselect-bottom");

            AddInternal(bottom != null
                ? new Sprite
                {
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                    Height = bottom.DisplayHeight,
                    Texture = bottom,
                }
                : new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 50 * STABLE_SCALE,
                    Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0.5f), Color4.Black.Opacity(0.8f)),
                });
        }
    }

    /// <summary>
    /// stable's mode button: <c>selection-mode</c> with its hover texture and the current ruleset's icon.
    /// Cycles through the rulesets on click, as lazer has no mode menu to open.
    /// </summary>
    public partial class LegacyModeButton : OsuClickableContainer, ISerialisableDrawable
    {
        public const float STABLE_X = 140 * LegacySongSelectComponent.STABLE_SCALE;

        public bool UsesFixedAnchor { get; set; }

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private Bindable<RulesetInfo>? ruleset { get; set; }

        private Sprite modeSprite = null!;
        private Sprite overSprite = null!;
        private Container iconContainer = null!;
        private Sprite rulesetIcon = null!;
        private Sprite rulesetIconFlash = null!;
        private Container fallbackIcon = null!;

        public LegacyModeButton()
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            X = STABLE_X;
            Size = new Vector2(57.6f, 56.25f) * LegacySongSelectComponent.STABLE_SCALE;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                modeSprite = new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                },
                overSprite = new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Alpha = 0,
                },
                iconContainer = new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.Centre,
                    // stable places the icon 28.8 units right of the button and 35 units above the screen's bottom.
                    Position = new Vector2(28.8f, -35) * LegacySongSelectComponent.STABLE_SCALE,
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        rulesetIcon = new Sprite
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Blending = BlendingParameters.Additive,
                        },
                        rulesetIconFlash = new Sprite
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Blending = BlendingParameters.Additive,
                            Alpha = 0,
                        },
                        // rulesets without a stable icon (like custom rulesets) show their own, at the size of stable's 32 unit icons.
                        fallbackIcon = new Container
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(26),
                        },
                    }
                },
                new BeatFlash { Flash = flashIcon },
            };

            Action = cycleRuleset;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += onSkinChanged;

            if (ruleset != null)
                ruleset.BindValueChanged(_ => updateRuleset());

            updateTextures();
        }

        private void onSkinChanged() => Schedule(updateTextures);

        private void updateTextures()
        {
            modeSprite.Texture = skin.GetTexture("selection-mode");
            overSprite.Texture = skin.GetTexture("selection-mode-over");
            updateRuleset();
        }

        private void updateRuleset()
        {
            Texture? icon = ruleset?.Value == null ? null : skin.GetTexture($"mode-{ruleset.Value.ShortName}-small");

            rulesetIcon.Texture = icon;
            rulesetIconFlash.Texture = icon;

            fallbackIcon.Clear();

            if (icon == null && ruleset?.Value != null)
            {
                var drawable = ruleset.Value.CreateInstance().CreateIcon();
                drawable.Anchor = drawable.Origin = Anchor.Centre;
                drawable.Size = fallbackIcon.Size;
                fallbackIcon.Add(drawable);
            }

            // like stable, the icon pops in whenever the mode changes.
            iconContainer.ScaleTo(0).ScaleTo(1, 1000, Easing.OutElastic);
        }

        private void flashIcon(double beatLength)
        {
            rulesetIconFlash.FadeTo(0.6f).FadeOut(Math.Max(beatLength, 100), Easing.Out);
        }

        private void cycleRuleset()
        {
            if (ruleset == null || ruleset.Disabled)
                return;

            var available = rulesets.AvailableRulesets.ToList();

            if (available.Count == 0)
                return;

            int index = available.FindIndex(r => r.Equals(ruleset.Value));
            ruleset.Value = available[(index + 1) % available.Count];
        }

        protected override bool OnHover(HoverEvent e)
        {
            overSprite.FadeIn(100, Easing.OutQuint);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            overSprite.FadeOut(100, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;
        }

        private partial class BeatFlash : BeatSyncedContainer
        {
            public Action<double>? Flash;

            protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
            {
                base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);
                Flash?.Invoke(timingPoint.BeatLength);
            }
        }
    }

    /// <summary>
    /// stable's beatmap information in the top left corner of song select.
    /// </summary>
    public partial class LegacyBeatmapDetails : CompositeDrawable, ISerialisableDrawable
    {
        private static readonly Color4 colour_increased = new Color4(246, 154, 161, 255);
        private static readonly Color4 colour_decreased = Color4.LightBlue;

        public bool UsesFixedAnchor { get; set; }

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private SkinManager? skinManager { get; set; }

        [Resolved]
        private OsuGame? game { get; set; }

        private readonly OsuSpriteText title;
        private readonly FillFlowContainer mapper;
        private readonly OsuSpriteText mapperName;
        private readonly OsuSpriteText lengthAndBpm;
        private readonly OsuSpriteText objectCounts;
        private readonly OsuSpriteText difficulty;
        private readonly Sprite statusIcon;
        private string? statusTexture;

        private CancellationTokenSource? cancellationSource;
        private IBindable<StarDifficulty>? starDifficulty;

        public LegacyBeatmapDetails()
        {
            AutoSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                statusIcon = new Sprite
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(12) * LegacySongSelectComponent.STABLE_SCALE,
                    Alpha = 0,
                },
                title = createText(new Vector2(24, -3), 18, FontWeight.Regular),
                mapper = new FillFlowContainer
                {
                    Position = new Vector2(25, 13) * LegacySongSelectComponent.STABLE_SCALE,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        createText(Vector2.Zero, 12, FontWeight.Regular).With(t => t.Text = "Mapped by "),
                        new OsuHoverContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            IdleColour = Color4.White,
                            HoverColour = new Color4(255, 204, 34, 255),
                            Action = () =>
                            {
                                if (!beatmap.IsDefault)
                                    game?.ShowUser(beatmap.Value.BeatmapInfo.Metadata.Author);
                            },
                            Child = mapperName = createText(Vector2.Zero, 12, FontWeight.Regular),
                        },
                    }
                },
                lengthAndBpm = createText(new Vector2(2.5f, 24), 12, FontWeight.Bold),
                objectCounts = createText(new Vector2(2.5f, 36), 12, FontWeight.Regular),
                difficulty = createText(new Vector2(2.5f, 48), 8, FontWeight.Regular),
            };
        }

        private static OsuSpriteText createText(Vector2 stablePosition, float stableSize, FontWeight weight) => new OsuSpriteText
        {
            Position = stablePosition * LegacySongSelectComponent.STABLE_SCALE,
            Font = OsuFont.GetFont(size: stableSize * LegacySongSelectComponent.STABLE_SCALE, weight: weight),
            Shadow = false,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatmap.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));
            ruleset.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));
            mods.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));
            updateDisplay();
        }

        private void updateDisplay()
        {
            cancellationSource?.Cancel();
            cancellationSource = new CancellationTokenSource();

            var working = beatmap.Value;
            var info = working.BeatmapInfo;

            if (beatmap.IsDefault)
            {
                foreach (var text in InternalChildren.OfType<OsuSpriteText>())
                    text.Text = string.Empty;
                mapper.Hide();
                statusIcon.FadeOut(500);
                return;
            }

            double rate = ModUtils.CalculateRateWithMods(mods.Value);
            Color4 rateColour = rate > 1 ? colour_increased : rate < 1 ? colour_decreased : Color4.White;

            updateStatusIcon(info.Status);

            title.Text = $"{info.Metadata.Artist} - {info.Metadata.Title} [{info.DifficultyName}]";
            mapperName.Text = info.Metadata.Author.Username;
            mapper.Alpha = string.IsNullOrEmpty(info.Metadata.Author.Username) ? 0 : 1;

            double length = info.Length / rate;
            var controlPoints = working.Beatmap.ControlPointInfo;
            string bpm = formatRange(controlPoints.BPMMinimum * rate, controlPoints.BPMMaximum * rate, 60000 / working.Beatmap.GetMostCommonBeatLength() * rate);

            lengthAndBpm.Text = $"Length: {(int)length / 60000}:{(int)length % 60000 / 1000:00} BPM: {bpm} Objects: {info.TotalObjectCount}";
            lengthAndBpm.Colour = rateColour;

            objectCounts.Text = string.Empty;
            updateObjectCounts(working, cancellationSource.Token);

            bool hardRock = mods.Value.Any(m => m is ModHardRock);
            bool easy = mods.Value.Any(m => m is ModEasy);
            difficulty.Colour = hardRock ? colour_increased : easy ? colour_decreased : Color4.White;

            starDifficulty = difficultyCache.GetBindableDifficulty(info, cancellationSource.Token, SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            starDifficulty.BindValueChanged(_ => updateDifficulty(), true);
        }

        private void updateDifficulty()
        {
            if (beatmap.IsDefault || ruleset.Value == null)
                return;

            var attributes = ruleset.Value.CreateInstance().GetBeatmapAttributesForDisplay(beatmap.Value.BeatmapInfo, mods.Value);
            string attributeText = string.Join(" ", attributes.Select(a => $"{a.Acronym}:{a.AdjustedValue:0.##}"));

            difficulty.Text = $"{attributeText} Star Rating: {starDifficulty?.Value.Stars ?? 0:0.##}★";
        }

        // stable's ranked status icons always come from its default skin. It hides the icon for unsubmitted maps,
        // but local maps get its question mark here so they are not left without one.
        private void updateStatusIcon(BeatmapOnlineStatus status)
        {
            string textureName = status switch
            {
                BeatmapOnlineStatus.Ranked => "selection-ranked",
                BeatmapOnlineStatus.Approved or BeatmapOnlineStatus.Qualified => "selection-approved",
                BeatmapOnlineStatus.Loved => "selection-loved",
                _ => "selection-question",
            };

            statusIcon.ClearTransforms();
            statusIcon.Rotation = 0;
            statusIcon.Scale = Vector2.One;

            if (textureName != statusTexture)
            {
                statusTexture = textureName;
                statusIcon.Texture = skinManager?.DefaultClassicSkin.GetTexture(textureName);
                statusIcon.FadeInFromZero(400);
            }
            else
                statusIcon.FadeIn(400);

            switch (status)
            {
                case BeatmapOnlineStatus.Ranked:
                case BeatmapOnlineStatus.Loved:
                    statusIcon.ScaleTo(0.92f).ScaleTo(1.4f, 200, Easing.In).Then().ScaleTo(1, 200, Easing.In);
                    break;

                case BeatmapOnlineStatus.Approved:
                case BeatmapOnlineStatus.Qualified:
                    statusIcon.RotateTo(MathHelper.RadiansToDegrees(-0.4f)).RotateTo(0, 400, Easing.Out);
                    statusIcon.ScaleTo(1.4f).ScaleTo(1, 400, Easing.Out);
                    break;
            }
        }

        private void updateObjectCounts(WorkingBeatmap working, CancellationToken cancellationToken)
        {
            var currentRuleset = ruleset.Value;

            Task.Run(() =>
            {
                string text = string.Join(" ", working.GetPlayableBeatmap(currentRuleset).GetStatistics().Select(s => $"{s.Name}: {s.Content}"));

                Schedule(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                        objectCounts.Text = text;
                });
            }, cancellationToken);
        }

        private static string formatRange(double min, double max, double common)
        {
            if (Math.Abs(max - min) < 0.5)
                return $"{Math.Round(common)}";

            return $"{Math.Round(min)}-{Math.Round(max)} ({Math.Round(common)})";
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            cancellationSource?.Cancel();
        }
    }
}
