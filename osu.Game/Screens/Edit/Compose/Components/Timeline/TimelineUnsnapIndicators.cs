// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    /// <summary>
    /// Displays an icon above each node (head, repeat or tail) of a hit object on the timeline which isn't snapped to the beat (like osu!stable).
    /// </summary>
    public partial class TimelineUnsnapIndicators : CompositeDrawable
    {
        private const float icon_size = 14;

        private const float in_piece_icon_size = 9;

        /// <summary>
        /// The maximum distance in milliseconds from a (truncated) tick for a node to still be considered snapped.
        /// osu!stable allows 2 ms.
        /// </summary>
        private const int snap_leniency = 1;

        private readonly HitObject hitObject;

        /// <summary>
        /// The piece displayed above the head of the hit object (like the slider velocity), if any.
        /// The head icon is displayed inside it, as there's no space above it in the timeline.
        /// </summary>
        private readonly HitObjectPointPiece? headPiece;

        [Resolved]
        private EditorBeatmap? editorBeatmap { get; set; }

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private BindableBeatDivisor? beatDivisor { get; set; }

        private Bindable<EditorUnsnapMode> unsnapMode = null!;

        private Bindable<string> unsnapDivisors = null!;

        /// <summary>
        /// The divisors a node may be snapped to without being considered unsnapped.
        /// </summary>
        private int[] allowedDivisors = Array.Empty<int>();

        private double[] displayedUnsnaps = Array.Empty<double>();

        public TimelineUnsnapIndicators(HitObject hitObject, HitObjectPointPiece? headPiece = null)
        {
            this.hitObject = hitObject;
            this.headPiece = headPiece;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            unsnapMode = config.GetBindable<EditorUnsnapMode>(OsuSetting.SlopEditorUnsnapMode);
            unsnapDivisors = config.GetBindable<string>(OsuSetting.SlopEditorUnsnapDivisors);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            unsnapMode.BindValueChanged(_ => updateAllowedDivisors());
            unsnapDivisors.BindValueChanged(_ => updateAllowedDivisors());
            beatDivisor?.BindValueChanged(_ => updateAllowedDivisors());
            updateAllowedDivisors();
        }

        private void updateAllowedDivisors()
        {
            switch (unsnapMode.Value)
            {
                case EditorUnsnapMode.Enabled:
                    allowedDivisors = unsnapDivisors.Value.Split(',')
                                                    .Select(d => int.TryParse(d.Trim(), out int divisor) ? divisor : 0)
                                                    .Where(d => d > 0)
                                                    .ToArray();
                    break;

                case EditorUnsnapMode.CurrentDivisor:
                    allowedDivisors = new[] { beatDivisor?.Value ?? editorBeatmap?.BeatmapInfo.BeatDivisor ?? 4 };
                    break;

                default:
                    allowedDivisors = Array.Empty<int>();
                    break;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (editorBeatmap == null)
                return;

            // Like osu!stable, nothing is marked as unsnapped without any allowed divisors.
            double[] unsnaps = allowedDivisors.Length == 0
                ? new double[getNodeTimes().Count()]
                : getNodeTimes().Select(getUnsnap).ToArray();

            if (unsnaps.SequenceEqual(displayedUnsnaps))
                return;

            displayedUnsnaps = unsnaps;

            ClearInternal();
            headPiece?.PrefixContainer.Clear();

            for (int i = 0; i < unsnaps.Length; i++)
            {
                if (unsnaps[i] == 0)
                    continue;

                var icon = new UnsnapIcon(unsnaps[i], unsnaps.Length > 1 ? getNodeName(i, unsnaps.Length) : null);

                if (i == 0 && headPiece != null)
                {
                    icon.Anchor = Anchor.CentreLeft;
                    icon.Origin = Anchor.CentreLeft;
                    icon.Size = new Vector2(in_piece_icon_size);
                    icon.Y = 0;
                    icon.Margin = new MarginPadding { Left = 5 };
                    icon.Colour = colours.B5;
                    icon.Shadow = false;

                    headPiece.PrefixContainer.Add(icon);
                    continue;
                }

                icon.RelativePositionAxes = Axes.X;
                icon.X = unsnaps.Length > 1 ? (float)i / (unsnaps.Length - 1) : 0;

                AddInternal(icon);
            }
        }

        /// <summary>
        /// Returns how far the node at <paramref name="time"/> is off the beat, or 0 if it counts as snapped.
        /// </summary>
        private double getUnsnap(double time)
        {
            if (isSnapped((int)time))
                return 0;

            return time - editorBeatmap!.ControlPointInfo.GetClosestSnappedTime(time);
        }

        /// <summary>
        /// Like osu!stable's timeline check, tick times are truncated to integers and a node is snapped if it is
        /// within <see cref="snap_leniency"/> of the tick before or after it for any of the <see cref="allowedDivisors"/>.
        /// </summary>
        private bool isSnapped(int time)
        {
            var timingPoint = editorBeatmap!.ControlPointInfo.TimingPointAt(time);

            foreach (int divisor in allowedDivisors)
            {
                double tickLength = timingPoint.BeatLength / divisor;
                double offsetTime = time - timingPoint.Time;

                int tickIndex = (int)(offsetTime / tickLength);
                if (offsetTime < 0)
                    tickIndex--;

                int previousTick = (int)(tickIndex * tickLength + timingPoint.Time);
                int nextTick = (int)((tickIndex + 1) * tickLength + timingPoint.Time);

                if (Math.Abs(time - previousTick) <= snap_leniency || Math.Abs(time - nextTick) <= snap_leniency)
                    return true;
            }

            return false;
        }

        private IEnumerable<double> getNodeTimes()
        {
            yield return hitObject.StartTime;

            if (hitObject is not IHasDuration hasDuration)
                yield break;

            if (hitObject is IHasRepeats hasRepeats)
            {
                double spanDuration = hasRepeats.Duration / (hasRepeats.RepeatCount + 1);

                for (int i = 0; i < hasRepeats.RepeatCount; i++)
                    yield return hitObject.StartTime + spanDuration * (i + 1);
            }

            yield return hasDuration.EndTime;
        }

        private static string getNodeName(int index, int count)
        {
            if (index == 0)
                return "Start";

            return index == count - 1 ? "End" : $"Repeat {index}";
        }

        private partial class UnsnapIcon : SpriteIcon, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            public UnsnapIcon(double unsnap, string? nodeName)
            {
                TooltipText = $"{(nodeName != null ? nodeName + " u" : "U")}nsnapped by {unsnap:+0.##;-0.##} ms";

                Anchor = Anchor.TopLeft;
                Origin = Anchor.BottomCentre;
                Size = new Vector2(icon_size);
                Y = -2;
                Icon = FontAwesome.Solid.Unlink;
                Colour = Color4.White;
                Shadow = true;
                ShadowColour = new Color4(0, 0, 0, 0.75f);
            }
        }
    }
}
