// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.PatternGallery
{
    /// <summary>
    /// A small drawing of the hitobjects of a pattern, in the style of its mode.
    /// </summary>
    public partial class PatternPreview : CompositeDrawable
    {
        private static readonly Color4[] default_combo_colours =
        {
            new Color4(255, 192, 0, 255),
            new Color4(0, 202, 0, 255),
            new Color4(18, 124, 255, 255),
            new Color4(242, 24, 57, 255),
        };

        private readonly PatternStore store;
        private readonly Pattern pattern;

        private Container canvas = null!;

        public PatternPreview(PatternStore store, Pattern pattern)
        {
            this.store = store;
            this.pattern = pattern;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource? skin)
        {
            var beatmap = store.Open(pattern);
            var comboColours = skin?.GetConfig<GlobalSkinColours, IReadOnlyList<Color4>>(GlobalSkinColours.ComboColours)?.Value is { Count: > 0 } colours
                ? colours
                : default_combo_colours;

            InternalChild = canvas = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };

            if (beatmap == null || beatmap.HitObjects.Count == 0)
                return;

            foreach (var h in beatmap.HitObjects)
            {
                try
                {
                    h.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                }
                catch
                {
                    // the duration of a broken slider is only cosmetic here.
                }
            }

            switch (pattern.RulesetId)
            {
                case 1:
                    drawTaiko(beatmap);
                    break;

                case 2:
                    drawCatch(beatmap, comboColours);
                    break;

                case 3:
                    drawMania(beatmap);
                    break;

                default:
                    drawOsu(beatmap, comboColours);
                    break;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (canvas.Width <= 0 || canvas.Height <= 0)
                return;

            canvas.Scale = new Vector2(Math.Min(DrawWidth / canvas.Width, DrawHeight / canvas.Height));
        }

        private static IEnumerable<(HitObject HitObject, Color4 Colour)> withComboColours(IBeatmap beatmap, IReadOnlyList<Color4> colours)
        {
            int index = -1;

            foreach (var h in beatmap.HitObjects)
            {
                if (index < 0 || (h is IHasCombo combo && combo.NewCombo) || h is IHasDuration and not IHasPath)
                    index += 1 + ((h as IHasCombo)?.ComboOffset ?? 0);

                yield return (h, colours[Math.Max(0, index) % colours.Count]);
            }
        }

        private void drawOsu(IBeatmap beatmap, IReadOnlyList<Color4> colours)
        {
            float radius = 54.4f - 4.48f * beatmap.Difficulty.CircleSize;
            var objects = withComboColours(beatmap, colours).ToList();

            // frame the objects rather than the whole playfield, with a minimum size so a lone circle is not huge.
            var points = objects.SelectMany(o => pointsOf(o.HitObject)).ToList();
            var min = new Vector2(points.Min(p => p.X), points.Min(p => p.Y)) - new Vector2(radius);
            var max = new Vector2(points.Max(p => p.X), points.Max(p => p.Y)) + new Vector2(radius);
            var size = Vector2.ComponentMax(max - min, new Vector2(256, 192));
            var offset = (min + max - size) / 2;

            canvas.Size = size;

            // later objects first, such that earlier ones are drawn on top like in gameplay.
            foreach (var (h, colour) in Enumerable.Reverse(objects))
            {
                var position = ((IHasPosition)h).Position - offset;

                switch (h)
                {
                    case IHasPath slider:
                        var path = new List<Vector2>();
                        slider.Path.GetPathToProgress(path, 0, 1);

                        if (path.Count > 1)
                        {
                            canvas.Add(createPath(path, position, radius, Color4.White));
                            canvas.Add(createPath(path, position, radius * 0.85f, colour.Darken(0.6f)));
                        }

                        canvas.Add(createCircle(position + slider.Path.PositionAt(1), radius, colour, 0.5f));
                        canvas.Add(createCircle(position, radius, colour));
                        break;

                    case IHasDuration:
                        canvas.Add(new CircularContainer
                        {
                            Origin = Anchor.Centre,
                            Position = new Vector2(256, 192) - offset,
                            Size = new Vector2(300),
                            Masking = true,
                            BorderThickness = 8,
                            BorderColour = Color4.White,
                            Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                        });
                        break;

                    default:
                        canvas.Add(createCircle(position, radius, colour));
                        break;
                }
            }

            static IEnumerable<Vector2> pointsOf(HitObject h)
            {
                if (h is IHasDuration and not IHasPath)
                    yield break;

                var position = ((IHasPosition)h).Position;
                yield return position;

                if (h is IHasPath slider)
                {
                    var path = new List<Vector2>();
                    slider.Path.GetPathToProgress(path, 0, 1);

                    foreach (var p in path)
                        yield return position + p;
                }
            }
        }

        private void drawCatch(IBeatmap beatmap, IReadOnlyList<Color4> colours)
        {
            var objects = withComboColours(beatmap, colours).ToList();
            double start = objects.Min(o => o.HitObject.StartTime);
            double end = objects.Max(o => o.HitObject.GetEndTime());
            double span = Math.Max(1, end - start);

            const float width = 512;
            const float fruit = 36;
            float timeScale = (float)Math.Min(0.3, 1.5 * width / span);
            float height = Math.Max((float)span * timeScale, 192) + fruit * 2;

            canvas.Size = new Vector2(width, height);
            canvas.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.2f });

            foreach (var (h, colour) in objects)
            {
                float yOf(double time) => height - fruit - (float)(time - start) * timeScale;
                float x = ((IHasPosition)h).Position.X;

                switch (h)
                {
                    case IHasPath stream:
                        double duration = Math.Max(1, h.GetEndTime() - h.StartTime);

                        for (double t = 0; t <= duration; t += Math.Max(duration / 24, 1))
                        {
                            // the path goes back and forth once per repeat.
                            double progress = t / duration * ((stream as IHasRepeats)?.SpanCount() ?? 1) % 2;
                            var p = stream.Path.PositionAt(Math.Clamp(progress > 1 ? 2 - progress : progress, 0, 1));
                            canvas.Add(createCircle(new Vector2(x + p.X, yOf(h.StartTime + t)), fruit / 4, colour));
                        }

                        canvas.Add(createCircle(new Vector2(x, yOf(h.StartTime)), fruit / 2, colour));
                        break;

                    case IHasDuration:
                        for (double t = h.StartTime; t <= h.GetEndTime(); t += 100)
                            canvas.Add(createCircle(new Vector2(width / 2, yOf(t)), fruit / 3, new Color4(250, 214, 61, 255)));
                        break;

                    default:
                        canvas.Add(createCircle(new Vector2(x, yOf(h.StartTime)), fruit / 2, colour));
                        break;
                }
            }
        }

        private void drawTaiko(IBeatmap beatmap)
        {
            var objects = beatmap.HitObjects;
            double start = objects.Min(h => h.StartTime);
            double span = Math.Max(1, objects.Max(h => h.GetEndTime()) - start);

            const float height = 120;
            const float note = 36;
            float timeScale = (float)Math.Min(0.5, 6 * height / span);
            float width = Math.Max((float)span * timeScale, 256) + note * 2;

            canvas.Size = new Vector2(width, height);
            canvas.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.3f });

            var don = new Color4(235, 69, 44, 255);
            var kat = new Color4(68, 141, 235, 255);
            var yellow = new Color4(252, 184, 6, 255);

            // later objects first, such that earlier ones are drawn on top like in gameplay.
            foreach (var h in objects.Reverse())
            {
                float x = note + (float)(h.StartTime - start) * timeScale;
                bool strong = h.Samples.Any(s => s.Name == HitSampleInfo.HIT_FINISH);
                float size = strong ? note * 1.4f : note;

                if (h is IHasDuration)
                {
                    float length = (float)(h.GetEndTime() - h.StartTime) * timeScale;

                    canvas.Add(new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = x - size / 2,
                        Size = new Vector2(length + size, size),
                        Masking = true,
                        CornerRadius = size / 2,
                        Child = new Box { RelativeSizeAxes = Axes.Both, Colour = yellow },
                    });
                    continue;
                }

                bool isKat = h.Samples.Any(s => s.Name is HitSampleInfo.HIT_WHISTLE or HitSampleInfo.HIT_CLAP);
                canvas.Add(createCircle(new Vector2(x, height / 2), size / 2, isKat ? kat : don));
            }
        }

        private void drawMania(IBeatmap beatmap)
        {
            var objects = beatmap.HitObjects;
            int keys = Math.Max(1, (int)Math.Round(beatmap.Difficulty.CircleSize));
            double start = objects.Min(h => h.StartTime);
            double span = Math.Max(1, objects.Max(h => h.GetEndTime()) - start);

            const float column = 40;
            const float note = 12;
            float width = keys * column;
            float timeScale = (float)Math.Min(0.4, 2.5 * Math.Max(width, 160) / span);
            float height = Math.Max((float)span * timeScale, width * 0.75f) + note * 2;

            canvas.Size = new Vector2(width, height);

            for (int i = 0; i < keys; i++)
            {
                canvas.Add(new Box
                {
                    X = i * column + 1,
                    Size = new Vector2(column - 2, height),
                    Colour = new Color4(48, 62, 66, 255),
                });
            }

            foreach (var h in objects)
            {
                int columnIndex = Math.Clamp((int)Math.Floor(((IHasPosition)h).Position.X * keys / 512), 0, keys - 1);
                float bottom = height - note - (float)(h.StartTime - start) * timeScale;
                float length = h is IHasDuration ? (float)(h.GetEndTime() - h.StartTime) * timeScale : 0;

                canvas.Add(new Container
                {
                    Origin = Anchor.BottomLeft,
                    Position = new Vector2(columnIndex * column + 3, bottom + note / 2),
                    Size = new Vector2(column - 6, length + note),
                    Masking = true,
                    CornerRadius = 3,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = h is IHasDuration ? new Color4(120, 190, 255, 255) : new Color4(232, 232, 232, 255),
                    },
                });
            }
        }

        private static Drawable createCircle(Vector2 position, float radius, Color4 colour, float alpha = 1) => new CircularContainer
        {
            Origin = Anchor.Centre,
            Position = position,
            Size = new Vector2(radius * 2),
            Alpha = alpha,
            Masking = true,
            BorderThickness = radius * 0.15f,
            BorderColour = Color4.White,
            Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
        };

        private static Drawable createPath(List<Vector2> points, Vector2 position, float radius, Color4 colour)
        {
            var path = new SmoothPath
            {
                PathRadius = radius,
                Vertices = points,
                Colour = colour,
            };

            path.OriginPosition = path.PositionInBoundingBox(Vector2.Zero);
            path.Position = position;
            return path;
        }
    }
}
