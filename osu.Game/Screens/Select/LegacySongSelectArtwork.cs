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
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
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
    /// stable's <c>songselect-top</c>, with its rightmost column repeated to fill wider screens.
    /// </summary>
    public partial class LegacySongSelectTop : LegacySongSelectComponent
    {
        public LegacySongSelectTop()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        protected override void UpdateArtwork()
        {
            ClearInternal();

            if (Skin.GetTexture("songselect-top") is not Texture top)
            {
                // lazer doesn't ship stable's default artwork, so keep the texts readable with a plain shade instead.
                AddInternal(new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 82 * STABLE_SCALE,
                    Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0.8f), Color4.Black.Opacity(0.5f)),
                });
                return;
            }

            var lastColumn = top.Crop(new RectangleF(top.Width - 1, 0, 1, top.Height));
            lastColumn.ScaleAdjust = top.ScaleAdjust;

            AddRangeInternal(new Drawable[]
            {
                new Sprite { Texture = top },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = top.DisplayHeight,
                    Padding = new MarginPadding { Left = top.DisplayWidth - 1 },
                    Child = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = lastColumn,
                    }
                },
            });
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

        private readonly OsuSpriteText title;
        private readonly OsuSpriteText mapper;
        private readonly OsuSpriteText lengthAndBpm;
        private readonly OsuSpriteText objectCounts;
        private readonly OsuSpriteText difficulty;

        private CancellationTokenSource? cancellationSource;
        private IBindable<StarDifficulty>? starDifficulty;

        public LegacyBeatmapDetails()
        {
            AutoSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                title = createText(new Vector2(21, -3), 18, FontWeight.Regular),
                mapper = createText(new Vector2(23, 13), 12, FontWeight.Regular),
                lengthAndBpm = createText(new Vector2(1, 24), 12, FontWeight.Bold),
                objectCounts = createText(new Vector2(1, 36), 12, FontWeight.Regular),
                difficulty = createText(new Vector2(1, 48), 8, FontWeight.Regular),
            };
        }

        private static OsuSpriteText createText(Vector2 stablePosition, float stableSize, FontWeight weight) => new OsuSpriteText
        {
            Position = stablePosition * LegacySongSelectComponent.STABLE_SCALE,
            // stable's font renders noticeably smaller than its nominal size.
            Font = OsuFont.GetFont(size: stableSize * LegacySongSelectComponent.STABLE_SCALE * 0.8f, weight: weight),
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
                return;
            }

            double rate = ModUtils.CalculateRateWithMods(mods.Value);
            Color4 rateColour = rate > 1 ? colour_increased : rate < 1 ? colour_decreased : Color4.White;

            title.Text = $"{info.Metadata.Artist} - {info.Metadata.Title} [{info.DifficultyName}]";
            mapper.Text = string.IsNullOrEmpty(info.Metadata.Author.Username) ? string.Empty : $"Mapped by {info.Metadata.Author.Username}";

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

            difficulty.Text = $"{attributeText} Stars:{starDifficulty?.Value.Stars ?? 0:0.##}";
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
