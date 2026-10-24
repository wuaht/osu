// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MapsetVerifier.Framework.Objects;
using MapsetVerifier.Snapshots;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using System.Globalization;
using System.Text.RegularExpressions;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Edit;
using osuTK;
using osuTK.Graphics;
using MvBeatmap = MapsetVerifier.Parser.Objects.Beatmap;

namespace osu.Game.MapsetVerifier.UI
{
    /// <summary>
    /// How Mapset Verifier shows issue levels, difficulty levels and modes.
    /// </summary>
    public static class VerifierDisplay
    {
        /// <summary>
        /// The issue levels in the order which they are shown in, most severe first.
        /// </summary>
        public static readonly Issue.Level[] LEVEL_ORDER = { Issue.Level.Error, Issue.Level.Problem, Issue.Level.Warning, Issue.Level.Minor, Issue.Level.Info };

        /// <summary>
        /// The order of the outcomes in the documentation.
        /// </summary>
        public static readonly Issue.Level[] OUTCOME_ORDER = { Issue.Level.Problem, Issue.Level.Warning, Issue.Level.Minor, Issue.Level.Info, Issue.Level.Check, Issue.Level.Error };

        /// <summary>
        /// How severe a level is, higher is more severe.
        /// </summary>
        public static int GetSeverity(Issue.Level level)
        {
            int index = Array.IndexOf(LEVEL_ORDER, GetDisplayLevel(level));
            return index < 0 ? 0 : LEVEL_ORDER.Length - index;
        }

        public static readonly Color4 PROBLEM = Color4Extensions.FromHex(@"fa5252");
        public static readonly Color4 WARNING = Color4Extensions.FromHex(@"fd7e14");
        public static readonly Color4 INFO = Color4Extensions.FromHex(@"12b886");
        public static readonly Color4 ERROR = Color4Extensions.FromHex(@"adb5bd");
        public static readonly Color4 CHECK = Color4Extensions.FromHex(@"40c057");

        public static readonly Color4 ADDED = Color4Extensions.FromHex(@"51cf66");
        public static readonly Color4 REMOVED = Color4Extensions.FromHex(@"ff6b6b");
        public static readonly Color4 CHANGED = Color4Extensions.FromHex(@"fcc419");

        public static readonly Color4 TIMESTAMP = Color4Extensions.FromHex(@"74c0fc");
        public static readonly Color4 SNAPSHOT_CHANGES = Color4Extensions.FromHex(@"4dabf7");

        public static Color4 GetColour(Issue.Level level)
        {
            switch (level)
            {
                case Issue.Level.Problem:
                    return PROBLEM;

                case Issue.Level.Warning:
                    return WARNING;

                case Issue.Level.Info:
                    return INFO;

                case Issue.Level.Error:
                    return ERROR;

                default:
                    return CHECK;
            }
        }

        public static IconUsage GetIcon(Issue.Level level)
        {
            switch (level)
            {
                case Issue.Level.Problem:
                    return FontAwesome.Solid.TimesCircle;

                case Issue.Level.Warning:
                    return FontAwesome.Solid.ExclamationTriangle;

                case Issue.Level.Minor:
                    return FontAwesome.Solid.MinusCircle;

                case Issue.Level.Info:
                    return FontAwesome.Solid.InfoCircle;

                case Issue.Level.Error:
                    return FontAwesome.Solid.Bug;

                default:
                    return FontAwesome.Solid.CheckCircle;
            }
        }

        public static LocalisableString GetName(Issue.Level level)
        {
            switch (level)
            {
                case Issue.Level.Problem:
                    return VerifierStrings.LevelProblem;

                case Issue.Level.Warning:
                    return VerifierStrings.LevelWarning;

                case Issue.Level.Minor:
                    return VerifierStrings.LevelMinor;

                case Issue.Level.Info:
                    return VerifierStrings.LevelInfo;

                case Issue.Level.Error:
                    return VerifierStrings.LevelError;

                default:
                    return VerifierStrings.LevelCheck;
            }
        }

        /// <summary>
        /// The level which an issue is shown as. Checks without any issue are shown as information.
        /// </summary>
        public static Issue.Level GetDisplayLevel(Issue.Level level) => level == Issue.Level.Check ? Issue.Level.Info : level;

        public static SpriteIcon CreateIcon(Issue.Level level, float size) => new SpriteIcon
        {
            Icon = GetIcon(level),
            Colour = GetColour(level),
            Size = new Vector2(size),
        };

        public static Color4 GetColour(MvBeatmap.Difficulty level)
        {
            switch (level)
            {
                case MvBeatmap.Difficulty.Easy:
                    return Color4Extensions.FromHex(@"4dabf7");

                case MvBeatmap.Difficulty.Normal:
                    return Color4Extensions.FromHex(@"51cf66");

                case MvBeatmap.Difficulty.Hard:
                    return Color4Extensions.FromHex(@"fcc419");

                case MvBeatmap.Difficulty.Insane:
                    return Color4Extensions.FromHex(@"ff6b6b");

                default:
                    return Color4Extensions.FromHex(@"cc5de8");
            }
        }

        /// <summary>
        /// The difficulty levels which a difficulty can be interpreted as.
        /// </summary>
        public static readonly MvBeatmap.Difficulty[] INTERPRETABLE_LEVELS =
        {
            MvBeatmap.Difficulty.Easy, MvBeatmap.Difficulty.Normal, MvBeatmap.Difficulty.Hard, MvBeatmap.Difficulty.Insane, MvBeatmap.Difficulty.Expert
        };

        /// <param name="level">The difficulty level.</param>
        /// <param name="mode">The mode of the difficulty.</param>
        /// <param name="useModeNames">Whether to use the names of the mode (e.g. "Kantan" in osu!taiko) instead of the generic ones.</param>
        public static string GetName(MvBeatmap.Difficulty level, MvBeatmap.Mode? mode, bool useModeNames)
        {
            if (useModeNames && mode == MvBeatmap.Mode.Taiko)
            {
                switch (level)
                {
                    case MvBeatmap.Difficulty.Easy: return @"Kantan";
                    case MvBeatmap.Difficulty.Normal: return @"Futsuu";
                    case MvBeatmap.Difficulty.Hard: return @"Muzukashii";
                    case MvBeatmap.Difficulty.Insane: return @"Oni";
                    default: return @"Inner Oni";
                }
            }

            if (useModeNames && mode == MvBeatmap.Mode.Catch)
            {
                switch (level)
                {
                    case MvBeatmap.Difficulty.Easy: return @"Cup";
                    case MvBeatmap.Difficulty.Normal: return @"Salad";
                    case MvBeatmap.Difficulty.Hard: return @"Platter";
                    case MvBeatmap.Difficulty.Insane: return @"Rain";
                    default: return @"Overdose";
                }
            }

            return level == MvBeatmap.Difficulty.Ultra ? nameof(MvBeatmap.Difficulty.Expert) : level.ToString();
        }

        public static LocalisableString GetName(MvBeatmap.Mode mode)
        {
            switch (mode)
            {
                case MvBeatmap.Mode.Taiko:
                    return @"osu!taiko";

                case MvBeatmap.Mode.Catch:
                    return @"osu!catch";

                case MvBeatmap.Mode.Mania:
                    return @"osu!mania";

                default:
                    return @"osu!";
            }
        }

        /// <summary>
        /// Creates the icon of a mode, which is the icon of its ruleset.
        /// </summary>
        public static Drawable CreateIcon(RulesetStore rulesets, MvBeatmap.Mode mode, float size)
        {
            var ruleset = rulesets.GetRuleset((int)mode)?.CreateInstance();
            var icon = ruleset?.CreateIcon() ?? new SpriteIcon { Icon = FontAwesome.Solid.QuestionCircle };

            icon.Size = new Vector2(size);
            return icon;
        }

        public static Color4 GetColour(Snapshotter.DiffType type)
        {
            switch (type)
            {
                case Snapshotter.DiffType.Added:
                    return ADDED;

                case Snapshotter.DiffType.Removed:
                    return REMOVED;

                default:
                    return CHANGED;
            }
        }

        public static IconUsage GetIcon(Snapshotter.DiffType type)
        {
            switch (type)
            {
                case Snapshotter.DiffType.Added:
                    return FontAwesome.Solid.Plus;

                case Snapshotter.DiffType.Removed:
                    return FontAwesome.Solid.Minus;

                default:
                    return FontAwesome.Solid.ExchangeAlt;
            }
        }

        /// <summary>
        /// Formats a time in the way the editor writes timestamps, e.g. "01:23:456".
        /// </summary>
        public static string FormatTime(double timeMs)
        {
            var time = TimeSpan.FromMilliseconds(Math.Max(0, timeMs));
            return $@"{(int)time.TotalMinutes:00}:{time.Seconds:00}:{time.Milliseconds:000}";
        }

        // numbers in scientific notation, or with more decimals than are meaningful to read.
        private static readonly Regex raw_number_regex = new Regex(@"(?<![\w.])-?\d+(?:\.\d+)?E[+-]?\d+\b|(?<![\w.])-?\d+\.\d{4,}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex infinity_regex = new Regex(@"(?<![\w-])-?Infinity\b", RegexOptions.Compiled);

        /// <summary>
        /// Makes the numbers which Mapset Verifier writes without formatting readable,
        /// e.g. "7.275957614183426E-12" becomes "0" and "Infinity" becomes "infinite".
        /// </summary>
        public static string FormatNumbers(string text)
        {
            text = raw_number_regex.Replace(text, m =>
            {
                if (!double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    return m.Value;

                // avoids "-0" for tiny negative values.
                double rounded = Math.Round(value, 3);
                return (rounded == 0 ? 0 : rounded).ToString(@"0.###", CultureInfo.InvariantCulture);
            });

            return infinity_regex.Replace(text, m => m.Value.StartsWith('-') ? @"-infinite" : @"infinite");
        }

        private static readonly Regex negative_timestamp_regex = new Regex(@"(?<![\d:])-\d+(?= - )", RegexOptions.Compiled);

        /// <summary>
        /// Finds the editor timestamps in the message of an issue, in the order which they appear in.
        /// Mapset Verifier writes times before the start of the beatmap as negative milliseconds, which are shown at the start of the beatmap.
        /// </summary>
        public static IEnumerable<TimestampMatch> FindTimestamps(string message)
        {
            var matches = new List<TimestampMatch>();

            foreach (Match match in EditorTimestampParser.TIME_REGEX_STRICT.Matches(message))
                matches.Add(new TimestampMatch(match.Index, match.Value, match.Value));

            foreach (Match match in negative_timestamp_regex.Matches(message))
                matches.Add(new TimestampMatch(match.Index, match.Value, @"00:00:000"));

            return matches.OrderBy(m => m.Index);
        }
    }
}
