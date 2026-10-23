// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Screens.Edit.Compose.Components
{
    /// <summary>
    /// An invisible drawable at the position the context menu was opened at, which hosts a <see cref="PropertyTransformerPopover"/>.
    /// </summary>
    public partial class PropertyTransformerAnchor : DraggablePopoverAnchor
    {
        private readonly IReadOnlyCollection<HitObject> objects;

        public PropertyTransformerAnchor(IReadOnlyCollection<HitObject> objects)
        {
            this.objects = objects;
        }

        protected override DraggablePopover CreatePopover() => new PropertyTransformerPopover(objects);
    }

    public partial class PropertyTransformerPopover : DraggablePopover
    {
        private const float label_width = 110;
        private const float input_width = 60;
        private const float operator_width = 16;
        private const float preview_width = 100;
        private const float row_height = 30;

        private readonly IReadOnlyCollection<HitObject> objects;

        private readonly List<PropertyRow> rows = new List<PropertyRow>();

        private PropertyRow bpmRow = null!;
        private PropertyRow sliderVelocityRow = null!;
        private PropertyRow volumeRow = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        public PropertyTransformerPopover(IReadOnlyCollection<HitObject> objects)
        {
            this.objects = objects;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AllowableAnchors = new[] { Anchor.CentreLeft, Anchor.CentreRight };

            var timingPoints = PropertyTransformer.GetAffectedTimingPoints(editorBeatmap, objects);
            double[] beatLengths = timingPoints.Select(p => p.BeatLength).ToArray();
            double[] velocities = objects.OfType<IHasSliderVelocity>().Select(h => h.SliderVelocityMultiplier).ToArray();
            int[] volumes = objects.SelectMany(PropertyTransformer.GetSampleLists).SelectMany(s => s).Select(s => s.Volume).ToArray();

            rows.Add(bpmRow = new PropertyRow(SlopEditorStrings.PropertyTransformerBpm.ToString(), beatLengths.Length > 0,
                t => formatRange(beatLengths.Select(b => 60000 / PropertyTransformer.TransformBeatLength(b, t)), v => v.ToString("0.##", CultureInfo.InvariantCulture))));
            rows.Add(sliderVelocityRow = new PropertyRow(SlopEditorStrings.PropertyTransformerSliderVelocity.ToString(), velocities.Length > 0,
                t => formatRange(velocities.Select(v => PropertyTransformer.TransformSliderVelocity(v, t)), v => $"x{v.ToString("0.##", CultureInfo.InvariantCulture)}")));
            rows.Add(volumeRow = new PropertyRow(SlopEditorStrings.PropertyTransformerVolume.ToString(), volumes.Length > 0,
                t => formatRange(volumes.Select(v => (double)PropertyTransformer.TransformVolume(v, t)), v => $"{v:0}%")));

            Child = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(8),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = SlopEditorStrings.PropertyTransformer,
                        Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                        Alpha = 0.6f,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(4),
                        Children = rows,
                    },
                    new RoundedButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 34,
                        Text = SlopEditorStrings.PropertyTransformerApply,
                        Action = apply,
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            ScheduleAfterChildren(() => rows.FirstOrDefault(r => r.Enabled)?.Focus());
        }

        private void apply()
        {
            if (rows.Any(r => r.Enabled && r.Transform == null))
                return;

            PropertyTransformer.Apply(editorBeatmap, objects,
                bpmRow.Enabled ? bpmRow.Transform!.Value : PropertyTransform.IDENTITY,
                sliderVelocityRow.Enabled ? sliderVelocityRow.Transform!.Value : PropertyTransform.IDENTITY,
                volumeRow.Enabled ? volumeRow.Transform!.Value : PropertyTransform.IDENTITY);

            this.HidePopover();
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == GlobalAction.Select && !e.Repeat)
            {
                apply();
                return true;
            }

            return base.OnPressed(e);
        }

        private static string formatRange(IEnumerable<double> values, Func<double, string> format)
        {
            double[] array = values.ToArray();

            if (array.Length == 0)
                return string.Empty;

            double min = array.Min();
            double max = array.Max();

            // like osu!stable, values which barely differ are shown as one.
            return max - min < 0.1 ? format(min) : $"{format(min)} ~ {format(max)}";
        }

        private partial class PropertyRow : GridContainer
        {
            public readonly bool Enabled;

            private readonly OsuTextBox multiplier;
            private readonly OsuTextBox additive;
            private readonly OsuSpriteText preview;

            private readonly Func<PropertyTransform, string> formatPreview;

            /// <summary>
            /// The transform which was entered, or null if any of the inputs is invalid.
            /// </summary>
            public PropertyTransform? Transform
            {
                get
                {
                    if (!tryParse(multiplier.Text, out double m) || !tryParse(additive.Text, out double a))
                        return null;

                    return new PropertyTransform(m, a);
                }
            }

            public PropertyRow(string label, bool enabled, Func<PropertyTransform, string> formatPreview)
            {
                Enabled = enabled;
                this.formatPreview = formatPreview;

                AutoSizeAxes = Axes.X;
                Height = row_height;
                Alpha = enabled ? 1 : 0.4f;

                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, label_width),
                    new Dimension(GridSizeMode.Absolute, operator_width),
                    new Dimension(GridSizeMode.Absolute, input_width),
                    new Dimension(GridSizeMode.Absolute, operator_width),
                    new Dimension(GridSizeMode.Absolute, input_width),
                    new Dimension(GridSizeMode.Absolute, preview_width),
                };

                Content = new[]
                {
                    new Drawable[]
                    {
                        createText(label),
                        createText("x"),
                        multiplier = createInput("1"),
                        createText("+"),
                        additive = createInput("0"),
                        preview = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Margin = new MarginPadding { Left = 8 },
                            Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                        },
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                multiplier.Current.BindValueChanged(_ => updatePreview());
                additive.Current.BindValueChanged(_ => updatePreview(), true);
            }

            public void Focus() => GetContainingFocusManager()?.ChangeFocus(multiplier);

            private void updatePreview()
            {
                if (!Enabled)
                {
                    preview.Text = string.Empty;
                    return;
                }

                preview.Text = Transform is PropertyTransform transform ? formatPreview(transform) : "?";
            }

            private OsuTextBox createInput(string text) => new OsuTextBox
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                RelativeSizeAxes = Axes.X,
                Height = row_height - 4,
                Text = text,
                SelectAllOnFocus = true,
                ReadOnly = !Enabled,
                CommitOnFocusLost = true,
            };

            private static OsuSpriteText createText(string text) => new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = text,
                Font = OsuFont.Default.With(size: 15),
            };

            private static bool tryParse(string text, out double value)
                => double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
        }
    }
}
