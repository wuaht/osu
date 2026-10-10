// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.BnTracker;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// The names, colours and icons of the BN Tracker values, used everywhere on the "request" screen.
    /// </summary>
    public static class NominatorsDisplay
    {
        public static LocalisableString GetName(BnNominationStatus status)
        {
            switch (status)
            {
                case BnNominationStatus.Declined:
                    return SlopNominatorsStrings.StatusDeclined;

                case BnNominationStatus.Pending:
                    return SlopNominatorsStrings.StatusPending;

                case BnNominationStatus.Maybe:
                    return SlopNominatorsStrings.StatusMaybe;

                case BnNominationStatus.Unlikely:
                    return SlopNominatorsStrings.StatusUnlikely;

                case BnNominationStatus.Accepted:
                    return SlopNominatorsStrings.StatusAccepted;

                default:
                    return SlopNominatorsStrings.StatusNotAsked;
            }
        }

        /// <summary>
        /// The colours of the BN Tracker website.
        /// </summary>
        public static Color4 GetColour(BnNominationStatus status)
        {
            switch (status)
            {
                case BnNominationStatus.Declined:
                    return Color4Extensions.FromHex(@"f87171");

                case BnNominationStatus.Pending:
                    return Color4Extensions.FromHex(@"38bdf8");

                case BnNominationStatus.Maybe:
                    return Color4Extensions.FromHex(@"eab308");

                case BnNominationStatus.Unlikely:
                    return Color4Extensions.FromHex(@"fb923c");

                case BnNominationStatus.Accepted:
                    return Color4Extensions.FromHex(@"4ade80");

                default:
                    return Color4Extensions.FromHex(@"6b7280");
            }
        }

        public static readonly Color4 REMOVED_COLOUR = Color4Extensions.FromHex(@"9d8ec2");

        public static readonly Color4 OPEN_COLOUR = Color4Extensions.FromHex(@"4ade80");

        public static readonly Color4 CLOSED_COLOUR = Color4Extensions.FromHex(@"6b7280");

        public static LocalisableString GetName(BnNominatorLevel level)
        {
            switch (level)
            {
                case BnNominatorLevel.Evaluator:
                    return SlopNominatorsStrings.LevelNat;

                case BnNominatorLevel.Probation:
                    return SlopNominatorsStrings.LevelProbation;

                default:
                    return SlopNominatorsStrings.LevelFull;
            }
        }

        /// <summary>
        /// The colours of the user groups on the osu! website.
        /// </summary>
        public static Color4 GetColour(BnNominatorLevel level)
        {
            switch (level)
            {
                case BnNominatorLevel.Evaluator:
                    return Color4Extensions.FromHex(@"fa3703");

                case BnNominatorLevel.Probation:
                    return Color4Extensions.FromHex(@"d6a7f9");

                default:
                    return Color4Extensions.FromHex(@"a347eb");
            }
        }

        public static LocalisableString GetName(BnGameMode mode)
        {
            switch (mode)
            {
                case BnGameMode.Osu:
                    return @"osu!";

                case BnGameMode.Taiko:
                    return @"osu!taiko";

                case BnGameMode.Catch:
                    return @"osu!catch";

                case BnGameMode.Mania:
                    return @"osu!mania";

                default:
                    return SlopNominatorsStrings.ModeGeneral;
            }
        }

        public static LocalisableString GetName(BnBeatmapRankStatus status)
        {
            switch (status)
            {
                case BnBeatmapRankStatus.Graveyard:
                    return SlopNominatorsStrings.RankStatusGraveyard;

                case BnBeatmapRankStatus.Wip:
                    return SlopNominatorsStrings.RankStatusWip;

                case BnBeatmapRankStatus.Pending:
                    return SlopNominatorsStrings.RankStatusPending;

                case BnBeatmapRankStatus.Qualified:
                    return SlopNominatorsStrings.RankStatusQualified;

                case BnBeatmapRankStatus.Ranked:
                    return SlopNominatorsStrings.RankStatusRanked;

                case BnBeatmapRankStatus.Approved:
                    return SlopNominatorsStrings.RankStatusApproved;

                case BnBeatmapRankStatus.Loved:
                    return SlopNominatorsStrings.RankStatusLoved;

                default:
                    return SlopNominatorsStrings.RankStatusUnknown;
            }
        }

        public static LocalisableString GetName(BnBeatmapPriority priority)
        {
            switch (priority)
            {
                case BnBeatmapPriority.Low:
                    return SlopNominatorsStrings.PriorityLow;

                case BnBeatmapPriority.Medium:
                    return SlopNominatorsStrings.PriorityMedium;

                case BnBeatmapPriority.High:
                    return SlopNominatorsStrings.PriorityHigh;

                default:
                    return SlopNominatorsStrings.PriorityNone;
            }
        }

        public static Color4 GetColour(BnBeatmapPriority priority)
        {
            switch (priority)
            {
                case BnBeatmapPriority.Low:
                    return Color4Extensions.FromHex(@"38bdf8");

                case BnBeatmapPriority.Medium:
                    return Color4Extensions.FromHex(@"eab308");

                case BnBeatmapPriority.High:
                    return Color4Extensions.FromHex(@"f87171");

                default:
                    return Color4Extensions.FromHex(@"6b7280");
            }
        }

        /// <summary>
        /// The ruleset ID of a mode, or <c>null</c> for <see cref="BnGameMode.General"/>.
        /// </summary>
        public static int? GetRulesetId(BnGameMode mode)
        {
            switch (mode)
            {
                case BnGameMode.Osu:
                    return 0;

                case BnGameMode.Taiko:
                    return 1;

                case BnGameMode.Catch:
                    return 2;

                case BnGameMode.Mania:
                    return 3;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Creates the icon of a mode, which is the icon of the ruleset (or a globe for modes not tied to a ruleset).
        /// </summary>
        public static Drawable CreateIcon(RulesetStore rulesets, BnGameMode mode, float size)
        {
            int? rulesetId = GetRulesetId(mode);
            var ruleset = rulesetId != null ? rulesets.GetRuleset(rulesetId.Value)?.CreateInstance() : null;

            var icon = ruleset?.CreateIcon() ?? new SpriteIcon { Icon = FontAwesome.Solid.Globe };

            icon.Size = new Vector2(size);
            icon.Anchor = Anchor.CentreLeft;
            icon.Origin = Anchor.CentreLeft;

            return icon;
        }

        /// <summary>
        /// Creates a user for displaying the avatar and cover of a nominator.
        /// </summary>
        public static APIUser CreateUser(BnNominator nominator) => new APIUser
        {
            Id = nominator.OsuId,
            Username = nominator.Username,
            AvatarUrl = string.IsNullOrEmpty(nominator.AvatarUrl) ? $@"https://a.ppy.sh/{nominator.OsuId}" : nominator.AvatarUrl,
            CoverUrl = nominator.CoverUrl,
        };

        /// <summary>
        /// Capitalises the first letter of a value from Mappers Guild, which are all lowercase.
        /// </summary>
        public static string Capitalise(string value) => string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
    }
}
