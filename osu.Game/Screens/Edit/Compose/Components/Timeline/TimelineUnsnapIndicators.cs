// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
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
        /// Unsnaps below this are ignored, as all object ends have these due to the way slider velocity works.
        /// </summary>
        private const double minimum_unsnap = 1;

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

        private double[] displayedUnsnaps = Array.Empty<double>();

        public TimelineUnsnapIndicators(HitObject hitObject, HitObjectPointPiece? headPiece = null)
        {
            this.hitObject = hitObject;
            this.headPiece = headPiece;

            RelativeSizeAxes = Axes.Both;
        }

        protected override void Update()
        {
            base.Update();

            if (editorBeatmap == null)
                return;

            double[] unsnaps = getNodeTimes().Select(getUnsnap).ToArray();

            if (unsnaps.SequenceEqual(displayedUnsnaps))
                return;

            displayedUnsnaps = unsnaps;

            ClearInternal();
            headPiece?.PrefixContainer.Clear();

            for (int i = 0; i < unsnaps.Length; i++)
            {
                if (Math.Abs(unsnaps[i]) < minimum_unsnap)
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

        private double getUnsnap(double time) => time - editorBeatmap!.ControlPointInfo.GetClosestSnappedTime(time);

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
