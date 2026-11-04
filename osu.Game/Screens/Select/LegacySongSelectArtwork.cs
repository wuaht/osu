// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
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
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Extensions;
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

            // stable's search background sits below this artwork, see LegacyFilterDisplay.SearchBackground.

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
    /// Opens stable's mode menu on click, or cycles through the rulesets where there is none.
    /// </summary>
    public partial class LegacyModeButton : ClickableContainer, ISerialisableDrawable
    {
        public const float STABLE_X = 140 * LegacySongSelectComponent.STABLE_SCALE;

        public bool UsesFixedAnchor { get; set; }

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private Bindable<RulesetInfo>? ruleset { get; set; }

        [Resolved]
        private LegacyModeMenu? menu { get; set; }

        [Resolved]
        private LegacyModeButtonLayer? layer { get; set; }

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

            Action = () =>
            {
                if (menu == null)
                {
                    cycleRuleset();
                    return;
                }

                PlayExpandSample();
                menu.Toggle();
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += onSkinChanged;

            if (ruleset != null)
                ruleset.BindValueChanged(_ => updateRuleset());

            updateTextures();

            // stable draws the button's artwork above the group tabs and the search line, which some skins cover with it.
            layer?.Display(this);

            if (menu != null)
                menu.Button = this;
        }

        /// <summary>
        /// stable's <c>select-expand</c>, which lazer's default skins only have as a song select sample.
        /// </summary>
        internal void PlayExpandSample() => (skin.GetSample(new SampleInfo("select-expand")) ?? audio.Samples.Get("SongSelect/select-expand"))?.Play();

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
            overSprite.FadeIn(100);
            skin.GetSample(new SampleInfo("click-short"))?.Play();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            overSprite.FadeOut(100);
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
    /// The layer of song select that shows the <see cref="LegacyModeButton"/>'s artwork at stable's depth,
    /// above the filter controls' group tabs but below their dropdowns.
    /// </summary>
    public partial class LegacyModeButtonLayer : Container
    {
        private Drawable? button;

        public void Display(Drawable modeButton)
        {
            Clear();
            button = modeButton;
            Add(modeButton.CreateProxy());
        }

        protected override void Update()
        {
            base.Update();

            // the button goes away with its skin.
            if (button != null && button.Parent == null)
            {
                Clear();
                button = null;
            }
        }
    }

    /// <summary>
    /// stable's mode menu, opened by the <see cref="LegacyModeButton"/>: a row per ruleset above the button, osu! at the bottom.
    /// Rulesets stable doesn't have go on top, and the menu scrolls once there are more than stable's four.
    /// While open, any click outside its rows closes it.
    /// </summary>
    public partial class LegacyModeMenu : CompositeDrawable
    {
        private const float scale = LegacySongSelectComponent.STABLE_SCALE;
        private const float row_height = 80;
        private const int visible_rows = 4;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        private readonly OsuScrollContainer scroll;
        private readonly FillFlowContainer<Row> rows;

        /// <summary>
        /// The button that opens the menu, which closes it with its sound when clicked again.
        /// </summary>
        internal LegacyModeButton? Button { get; set; }

        public bool IsOpen { get; private set; }

        public LegacyModeMenu()
        {
            RelativeSizeAxes = Axes.Both;

            // stable's rows start at its mode button and stack upwards from 56 units above the screen's bottom.
            InternalChild = scroll = new OsuScrollContainer
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = LegacyModeButton.STABLE_X,
                Y = -56 * scale,
                Size = new Vector2(280, row_height * visible_rows) * scale,
                ScrollbarVisible = false,
                Child = rows = new FillFlowContainer<Row>
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var available = rulesets.AvailableRulesets.ToList();
            var bottomToTop = available.Where(r => r.IsLegacyRuleset()).OrderBy(r => r.OnlineID).Concat(available.Where(r => !r.IsLegacyRuleset())).ToList();

            for (int i = bottomToTop.Count - 1; i >= 0; i--)
            {
                var r = bottomToTop[i];

                rows.Add(new Row(r, i)
                {
                    Action = () => select(r),
                });
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += onSkinChanged;
            updateIcons();
        }

        private void onSkinChanged() => Schedule(updateIcons);

        private void updateIcons()
        {
            foreach (var row in rows)
                row.Icon = skin.GetTexture($"mode-{row.Ruleset.ShortName}-med");
        }

        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        public void Open()
        {
            if (IsOpen)
                return;

            IsOpen = true;
            scroll.ScrollToEnd(false);

            // stable shows the rows from the bottom up, 20ms apart.
            foreach (var row in rows)
                row.Appear(row.Index * 20);
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;

            foreach (var row in rows)
                row.Disappear();
        }

        private void select(RulesetInfo r)
        {
            if (!ruleset.Disabled)
                ruleset.Value = r;

            Close();
        }

        public override bool PropagatePositionalInputSubTree => IsOpen && base.PropagatePositionalInputSubTree;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => IsOpen;

        protected override bool OnMouseDown(MouseDownEvent e) => IsOpen;

        protected override bool OnClick(ClickEvent e)
        {
            if (!IsOpen)
                return false;

            if (Button?.ReceivePositionalInputAt(e.ScreenSpaceMousePosition) == true)
                Button.PlayExpandSample();

            Close();
            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;
        }

        /// <summary>
        /// stable's <c>pSpriteMenuItem</c>: a dark 280x80 row that turns purple on hover, with the mode's icon and name.
        /// </summary>
        private partial class Row : CompositeDrawable
        {
            private static readonly Color4 background_colour = new Color4(0, 0, 0, 160);
            private static readonly Color4 highlight_colour = new Color4(165, 90, 234, 230);

            private const float text_x = 90;

            public readonly RulesetInfo Ruleset;
            public readonly int Index;
            public Action? Action { get; init; }

            [Resolved]
            private ISkinSource skin { get; set; } = null!;

            private readonly Box background;
            private readonly Sprite icon;
            private readonly Container fallbackIcon;
            private readonly OsuSpriteText text;

            public Row(RulesetInfo ruleset, int index)
            {
                Ruleset = ruleset;
                Index = index;

                RelativeSizeAxes = Axes.X;
                Height = row_height * scale;

                InternalChildren = new Drawable[]
                {
                    // stable's colour alpha also scales the sprite's alpha, which fades in to the same value.
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = background_colour,
                        Alpha = 0,
                    },
                    icon = new Sprite
                    {
                        Origin = Anchor.Centre,
                        Position = new Vector2(40) * scale,
                        Alpha = 0,
                    },
                    // lazer's default skins have no mode-*-med icons, so rulesets show their own at the size of stable's.
                    fallbackIcon = new Container
                    {
                        Origin = Anchor.Centre,
                        Position = new Vector2(40) * scale,
                        Size = new Vector2(62 * scale),
                        Alpha = 0,
                        Child = ruleset.CreateInstance().CreateIcon().With(d =>
                        {
                            d.Anchor = d.Origin = Anchor.Centre;
                            d.RelativeSizeAxes = Axes.Both;
                        }),
                    },
                    text = new OsuSpriteText
                    {
                        Origin = Anchor.CentreLeft,
                        Position = new Vector2(text_x, 40) * scale,
                        Text = ruleset.Name,
                        Font = OsuFont.GetFont(size: 24 * LegacyUserPanel.StableText.EM * scale, weight: FontWeight.Bold),
                        Shadow = true,
                        Alpha = 0,
                    },
                };
            }

            public Texture? Icon
            {
                set
                {
                    icon.Texture = value;
                    icon.Size = value?.DisplaySize ?? Vector2.Zero;
                    fallbackIcon.Size = value == null ? new Vector2(62 * scale) : Vector2.Zero;
                }
            }

            public void Appear(double delay)
            {
                background.Delay(delay).FadeTo(background_colour.A, 200);
                icon.Delay(delay).FadeIn(200);
                fallbackIcon.Delay(delay).FadeIn(200);
                text.Delay(delay).FadeIn(200);
            }

            public void Disappear()
            {
                background.FadeOut(100);
                icon.FadeOut(100);
                fallbackIcon.FadeOut(100);
                text.FadeOut(100);
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeColour(highlight_colour, 10);
                // stable only bounces its own default icons.
                fallbackIcon.ScaleTo(1.1f, 1000, Easing.OutElastic);
                text.MoveToX((text_x + 10) * scale, 300, Easing.Out);
                skin.GetSample(new SampleInfo("click-short"))?.Play();
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeColour(background_colour, 100);
                fallbackIcon.ScaleTo(1, 1000, Easing.OutElastic);
                text.MoveToX(text_x * scale, 300, Easing.Out);
            }

            protected override bool OnMouseDown(MouseDownEvent e) => true;

            protected override bool OnClick(ClickEvent e)
            {
                skin.GetSample(new SampleInfo("click-short-confirm"))?.Play();
                Action?.Invoke();
                return true;
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
                case BeatmapOnlineStatus.Qualified:
                case BeatmapOnlineStatus.Loved:
                    statusIcon.ScaleTo(0.92f).ScaleTo(1.4f, 200, Easing.In).Then().ScaleTo(1, 200, Easing.In);
                    break;

                case BeatmapOnlineStatus.Approved:
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
