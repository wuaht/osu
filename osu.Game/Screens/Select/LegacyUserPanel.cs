// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// stable's user panel next to the song select buttons (avatar, name, performance, accuracy, level and rank).
    /// </summary>
    public partial class LegacyUserPanel : LegacySongSelectComponent
    {
        private const float scale = STABLE_SCALE;

        // stable's level bar fills 198 texture pixels, which is 198 units here (a 1x texture pixel is 1/1.6 stable units).
        private const float level_bar_width = 198;

        private static FontUsage font(float stableSize, FontWeight weight = FontWeight.Regular) => OsuFont.GetFont(size: stableSize * scale, weight: weight);

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private LocalUserStatisticsProvider? statisticsProvider { get; set; }

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

        private readonly UpdateableAvatar avatar;
        private readonly OsuSpriteText name;
        private readonly OsuTextFlowContainer info;
        private readonly OsuSpriteText rank;
        private readonly Container levelBar;
        private readonly Sprite levelBarBackground;
        private readonly Container levelProgress;
        private readonly Sprite levelBarForeground;

        public LegacyUserPanel()
        {
            // stable draws the panel at (389.6, 429), right of the last footer button.
            // Its sprites render about (5, 4.5) further in, measured against stable at 1080p.
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.TopLeft;
            Position = new Vector2(389.6f + 5, 429 + 4.5f - 480) * scale;
            Size = new Vector2(204, 47) * scale;

            InternalChildren = new Drawable[]
            {
                avatar = new UpdateableAvatar(isInteractive: false)
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(23) * scale,
                    // stable shows the avatar as a circle of about 42 units.
                    Size = new Vector2(42 * scale),
                    Masking = true,
                    CornerRadius = 21 * scale,
                },
                name = new OsuSpriteText
                {
                    Position = new Vector2(48, -2) * scale,
                    Font = font(14),
                    Shadow = true,
                },
                info = new OsuTextFlowContainer(t =>
                {
                    t.Font = font(10);
                    t.Shadow = true;
                })
                {
                    Position = new Vector2(48, 12) * scale,
                    AutoSizeAxes = Axes.Both,
                },
                rank = new OsuSpriteText
                {
                    Origin = Anchor.TopRight,
                    Position = new Vector2(204, 7) * scale,
                    Font = font(36, FontWeight.Light),
                },
                // stable draws the level bar textures additively, 120x62 texture pixels from the panel origin.
                levelBar = new Container
                {
                    Position = new Vector2(120, 62),
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        levelBarBackground = new Sprite
                        {
                            Blending = BlendingParameters.Additive,
                            Alpha = 0.4f,
                        },
                        levelProgress = new Container
                        {
                            AutoSizeAxes = Axes.Y,
                            Masking = true,
                            Child = levelBarForeground = new Sprite
                            {
                                Blending = BlendingParameters.Additive,
                                Alpha = 0.7f,
                                Colour = new Color4(252, 184, 6, 255),
                            },
                        },
                    }
                },
            };
        }

        protected override void UpdateArtwork()
        {
            levelBarBackground.Texture = Skin.GetTexture("levelbar-bg");
            levelBarForeground.Texture = Skin.GetTexture("levelbar");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            localUser.BindTo(api.LocalUser);
            localUser.BindValueChanged(_ => updateDisplay());
            ruleset.BindValueChanged(_ => updateDisplay(), true);

            if (statisticsProvider != null)
                statisticsProvider.StatisticsUpdated += onStatisticsUpdated;
        }

        private void onStatisticsUpdated(UserStatisticsUpdate update) => Schedule(() =>
        {
            if (update.Ruleset.Equals(ruleset.Value))
                updateDisplay();
        });

        private void updateDisplay()
        {
            var user = localUser.Value;
            var statistics = statisticsProvider?.GetStatisticsFor(ruleset.Value);

            avatar.User = user;
            name.Text = user.Username;

            if (statistics == null)
            {
                info.Text = string.Empty;
                rank.Text = string.Empty;
                levelBar.Hide();
                return;
            }

            levelBar.Show();
            levelProgress.Width = level_bar_width * statistics.Level.Progress / 100f;

            if (statistics.PP > 0)
                info.Text = string.Format(CultureInfo.InvariantCulture, "Performance:{0:#,0}pp\nAccuracy:{1:0.00}%\nLv{2}", statistics.PP, statistics.Accuracy, statistics.Level.Current);
            else if (statistics.RankedScore > 0)
                info.Text = string.Format(CultureInfo.InvariantCulture, "Score:{0}\nAccuracy:{1:0.00}%\nLv{2}", formatScore(statistics.RankedScore), statistics.Accuracy, statistics.Level.Current);
            else
                info.Text = "Never played";

            int? globalRank = statistics.GlobalRank;
            rank.Text = globalRank > 0 ? $"#{globalRank}" : string.Empty;
            rank.Colour = rankColour(globalRank ?? 0);
        }

        private static string formatScore(long score)
        {
            (double value, string suffix) = score > 1_000_000_000 ? (score / 1e9, "b") : score > 1_000_000 ? (score / 1e6, "m") : (score / 1e3, "k");
            return value.ToString(value % 1 != 0 ? "#,0.0" : "#,0", CultureInfo.InvariantCulture) + suffix;
        }

        // stable fades the rank in as it improves, with gold and blue for the very top.
        private static Color4 rankColour(int rank)
        {
            if (rank > 100000) return Color4.White.Opacity(40 / 255f);
            if (rank > 50000) return Color4.White.Opacity(60 / 255f);
            if (rank > 1000) return Color4.White.Opacity(80 / 255f);
            if (rank > 10) return Color4.White.Opacity(100 / 255f);
            if (rank > 1) return new Color4(244, 218, 73, 120);

            return new Color4(88, 171, 248, 120);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (statisticsProvider.IsNotNull())
                statisticsProvider.StatisticsUpdated -= onStatisticsUpdated;
        }
    }
}
