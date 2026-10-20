// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit.Streams
{
    /// <summary>
    /// Keeps track of the editor-only <see cref="EditorStream"/>s of the beatmap and keeps them in sync with their hit circles.
    /// </summary>
    /// <remarks>
    /// Moving, rotating, scaling, mirroring or time-shifting all circles of a stream together is applied to the stream.
    /// Any other change to its circles turns them into regular hit circles.
    /// </remarks>
    public partial class EditorStreamManager : Component
    {
        /// <summary>
        /// Leniency for matching circles to the stream they belong to, to account for rounding when the beatmap is encoded.
        /// </summary>
        private const double time_leniency = 2;

        private const float position_leniency = 2;

        /// <summary>
        /// The stream whose circles are exactly the current selection, if any.
        /// </summary>
        public readonly Bindable<EditorStream?> SelectedStream = new Bindable<EditorStream?>();

        /// <summary>
        /// Invoked when a stream's path or circles changed, or the set of streams changed.
        /// </summary>
        public event Action? StreamsChanged;

        public IReadOnlyList<EditorStream> Streams => streams;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler? changeHandler { get; set; }

        private readonly List<EditorStream> streams = new List<EditorStream>();

        private Bindable<string[]> slopStreams = null!;

        /// <summary>
        /// The last value written to <see cref="EditorBeatmap.SlopStreams"/>, to tell own changes apart from external ones (undo, redo).
        /// </summary>
        private string[]? lastWritten;

        private bool applying;
        private bool validating;
        private bool dirty;

        private readonly HashSet<EditorStream> pendingLayouts = new HashSet<EditorStream>();

        private readonly HashSet<HitObject> pendingSelected = new HashSet<HitObject>();
        private readonly HashSet<HitObject> pendingDeselected = new HashSet<HitObject>();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            slopStreams = editorBeatmap.SlopStreams.GetBoundCopy();
            slopStreams.BindValueChanged(s =>
            {
                if (!ReferenceEquals(s.NewValue, lastWritten))
                    load(s.NewValue);
            }, true);

            editorBeatmap.TransactionEnding += onTransactionEnding;

            // most edits (e.g. moving the selection) are only wrapped in a change of the change handler.
            if (changeHandler is TransactionalCommitComponent changeHandlerTransactions)
            {
                changeHandlerTransactions.TransactionEnding += onTransactionEnding;
                changeHandlerTransactions.SaveStateTriggered += onTransactionEnding;
            }

            editorBeatmap.SelectedHitObjects.CollectionChanged += onSelectionChanged;
        }

        /// <summary>
        /// Returns the stream <paramref name="hitObject"/> belongs to, if any.
        /// </summary>
        public EditorStream? GetStream(HitObject hitObject) => hitObject is HitCircle circle ? streams.FirstOrDefault(s => s.Circles.Contains(circle)) : null;

        /// <summary>
        /// Adds a new stream to the beatmap, replacing existing objects at the times of its circles.
        /// </summary>
        public void Add(EditorStream stream, IList<HitSampleInfo> samples, bool newCombo)
        {
            var layout = generate(stream);

            editorBeatmap.BeginChange();
            applying = true;

            var replaced = editorBeatmap.HitObjects.Where(h => layout.Any(c => Math.Abs(c.Time - h.StartTime) <= time_leniency)).ToArray();
            editorBeatmap.RemoveRange(replaced);

            var circles = layout.Select((c, i) => new HitCircle
            {
                StartTime = c.Time,
                Position = c.Position,
                NewCombo = i == 0 && newCombo,
                Samples = samples.Select(s => s.With()).ToList(),
            }).ToArray();

            editorBeatmap.AddRange(circles);

            stream.Circles.AddRange(circles);
            takeSnapshot(stream);
            streams.Add(stream);
            dirty = true;

            applying = false;
            editorBeatmap.EndChange();

            StreamsChanged?.Invoke();
        }

        /// <summary>
        /// Lays out the circles of <paramref name="stream"/> again after its path, densities or divisor changed.
        /// </summary>
        /// <remarks>
        /// This is debounced, but always applied before the current change ends.
        /// </remarks>
        public void Invalidate(EditorStream stream)
        {
            pendingLayouts.Add(stream);
            Scheduler.AddOnce(flushLayouts);
        }

        /// <summary>
        /// Immediately lays out all streams which were invalidated.
        /// </summary>
        public void ApplyPendingLayouts() => flushLayouts();

        /// <summary>
        /// Removes the stream, keeping its circles as regular hit circles.
        /// </summary>
        public void ConvertToCircles(EditorStream stream)
        {
            editorBeatmap.BeginChange();
            remove(stream);
            editorBeatmap.EndChange();
        }

        /// <summary>
        /// Deletes the stream along with its circles.
        /// </summary>
        public void Delete(EditorStream stream)
        {
            editorBeatmap.BeginChange();
            editorBeatmap.RemoveRange(stream.Circles.ToArray());
            remove(stream);
            editorBeatmap.EndChange();
        }

        private void remove(EditorStream stream)
        {
            if (!streams.Remove(stream))
                return;

            pendingLayouts.Remove(stream);
            dirty = true;

            updateSelectedStream();
            StreamsChanged?.Invoke();
        }

        #region Loading

        private void load(string[] lines)
        {
            streams.Clear();
            pendingLayouts.Clear();

            var claimed = new HashSet<HitObject>();
            bool dropped = false;

            foreach (string line in lines)
            {
                var stream = EditorStream.Parse(line);

                if (stream == null || !resolve(stream, claimed))
                {
                    dropped = true;
                    continue;
                }

                streams.Add(stream);
            }

            // streams whose circles can't be found anymore are dropped for good.
            if (dropped)
                write();

            // undo / redo recreates circles, so a stream may now only be partially selected.
            foreach (var h in editorBeatmap.SelectedHitObjects)
                pendingSelected.Add(h);

            updateSelectedStream();
            Scheduler.AddOnce(syncSelection);
            StreamsChanged?.Invoke();
        }

        /// <summary>
        /// Finds the hit circles of <paramref name="stream"/> in the beatmap.
        /// </summary>
        private bool resolve(EditorStream stream, HashSet<HitObject> claimed)
        {
            var hitObjects = editorBeatmap.HitObjects;

            foreach (var (time, generatedPosition) in generate(stream))
            {
                // the decoder clamps the positions of hit objects (see ConvertHitObjectParser), so circles outside of it don't match otherwise.
                Vector2 position = Vector2.Clamp(generatedPosition, Vector2.Zero, new Vector2(512));

                HitCircle? match = null;

                for (int i = lowerBound(hitObjects, time - time_leniency); i < hitObjects.Count && hitObjects[i].StartTime <= time + time_leniency; i++)
                {
                    if (hitObjects[i] is HitCircle circle && !claimed.Contains(circle) && Vector2.Distance(circle.Position, position) <= position_leniency)
                    {
                        match = circle;
                        break;
                    }
                }

                if (match == null)
                    return false;

                claimed.Add(match);
                stream.Circles.Add(match);
            }

            takeSnapshot(stream);
            return true;
        }

        private static int lowerBound(IReadOnlyList<HitObject> hitObjects, double time)
        {
            int low = 0;
            int high = hitObjects.Count;

            while (low < high)
            {
                int mid = (low + high) / 2;

                if (hitObjects[mid].StartTime < time)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }

        #endregion

        #region Layout

        private void flushLayouts()
        {
            if (pendingLayouts.Count == 0)
                return;

            var layouts = pendingLayouts.ToArray();
            pendingLayouts.Clear();

            editorBeatmap.BeginChange();

            foreach (var stream in layouts)
            {
                if (streams.Contains(stream))
                    layout(stream);
            }

            editorBeatmap.EndChange();

            StreamsChanged?.Invoke();
        }

        private void layout(EditorStream stream)
        {
            var layout = generate(stream);
            var circles = stream.Circles;

            bool wasSelected = SelectedStream.Value == stream;

            applying = true;

            // existing circles are updated in place, as re-adding all of them on every change makes them flicker.
            if (circles.Count > layout.Count)
            {
                var surplus = circles.GetRange(layout.Count, circles.Count - layout.Count);
                circles.RemoveRange(layout.Count, surplus.Count);
                editorBeatmap.RemoveRange(surplus);
            }

            var added = new List<HitCircle>();

            for (int i = 0; i < layout.Count; i++)
            {
                var (time, position) = layout[i];

                if (i < circles.Count)
                {
                    circles[i].StartTime = time;
                    circles[i].Position = position;
                    editorBeatmap.Update(circles[i]);
                }
                else
                {
                    var template = circles.LastOrDefault();

                    added.Add(new HitCircle
                    {
                        StartTime = time,
                        Position = position,
                        Samples = template?.Samples.Select(s => s.With()).ToList() ?? new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) },
                    });
                }
            }

            circles.AddRange(added);
            editorBeatmap.AddRange(added);

            if (wasSelected && added.Count > 0)
                editorBeatmap.SelectedHitObjects.AddRange(added);

            takeSnapshot(stream);
            dirty = true;

            applying = false;
        }

        private List<(double Time, Vector2 Position)> generate(EditorStream stream) => stream.Generate(editorBeatmap.ControlPointInfo, editorBeatmap.Difficulty.SliderMultiplier);

        private static void takeSnapshot(EditorStream stream) => stream.Snapshot = stream.Circles.Select(c => (c.StartTime, c.Position)).ToArray();

        #endregion

        #region Validation

        private void onTransactionEnding()
        {
            if (applying || validating)
                return;

            validating = true;

            try
            {
                validateAll();
            }
            finally
            {
                validating = false;
            }
        }

        private void validateAll()
        {
            // pending layouts are part of the change that caused them.
            flushLayouts();

            if (streams.Count > 0)
            {
                var hitObjects = new HashSet<HitObject>(editorBeatmap.HitObjects);
                bool removed = false;

                foreach (var stream in streams.ToArray())
                {
                    switch (validate(stream, hitObjects))
                    {
                        case ValidationResult.Changed:
                            dirty = true;
                            break;

                        case ValidationResult.Invalid:
                            streams.Remove(stream);
                            pendingLayouts.Remove(stream);
                            dirty = true;
                            removed = true;
                            break;
                    }
                }

                if (removed)
                {
                    updateSelectedStream();
                    StreamsChanged?.Invoke();
                }
            }

            if (dirty)
                write();
        }

        private ValidationResult validate(EditorStream stream, HashSet<HitObject> hitObjects)
        {
            var circles = stream.Circles;
            var snapshot = stream.Snapshot;

            if (circles.Count == 0 || circles.Count != snapshot.Length || circles.Any(c => !hitObjects.Contains(c)))
                return ValidationResult.Invalid;

            bool unchanged = true;

            for (int i = 0; i < circles.Count; i++)
            {
                if (Math.Abs(circles[i].StartTime - snapshot[i].Time) > 0.01 || Vector2.Distance(circles[i].Position, snapshot[i].Position) > 0.01f)
                {
                    unchanged = false;
                    break;
                }
            }

            if (unchanged)
                return ValidationResult.Unchanged;

            // the circles may only be shifted in time all together.
            double timeOffset = circles[0].StartTime - snapshot[0].Time;

            for (int i = 1; i < circles.Count; i++)
            {
                if (Math.Abs(circles[i].StartTime - snapshot[i].Time - timeOffset) > 1)
                    return ValidationResult.Invalid;
            }

            if (!tryFindSimilarity(stream, out var transform, out float scale))
                return ValidationResult.Invalid;

            stream.Path.StartTime += timeOffset;
            stream.Transform(transform, scale);

            takeSnapshot(stream);
            StreamsChanged?.Invoke();
            return ValidationResult.Changed;
        }

        /// <summary>
        /// Finds a transformation consisting of a translation, rotation, uniform scale and optionally a reflection which maps the snapshot of the circles to their current positions.
        /// </summary>
        private static bool tryFindSimilarity(EditorStream stream, out Func<Vector2, Vector2> transform, out float scale)
        {
            var circles = stream.Circles;
            var snapshot = stream.Snapshot;

            Vector2 from0 = snapshot[0].Position;
            Vector2 to0 = circles[0].Position;
            Vector2 fromSpan = snapshot[^1].Position - from0;
            Vector2 toSpan = circles[^1].Position - to0;

            if (fromSpan.LengthSquared < 0.01f)
            {
                Vector2 offset = to0 - from0;
                transform = p => p + offset;
                scale = 1;
                return matches(transform);
            }

            foreach (bool reflect in new[] { false, true })
            {
                Vector2 from = reflect ? conjugate(fromSpan) : fromSpan;

                // the complex factor which rotates and scales "from" onto "toSpan".
                Vector2 factor = new Vector2(
                    (toSpan.X * from.X + toSpan.Y * from.Y) / from.LengthSquared,
                    (toSpan.Y * from.X - toSpan.X * from.Y) / from.LengthSquared);

                if (Vector2.Distance(factor, Vector2.UnitX) < 1e-4f)
                    factor = Vector2.UnitX;

                Vector2 f = factor;
                bool r = reflect;

                Func<Vector2, Vector2> candidate = p =>
                {
                    Vector2 relative = p - from0;
                    if (r) relative = conjugate(relative);
                    return to0 + multiply(f, relative);
                };

                if (matches(candidate))
                {
                    transform = candidate;
                    scale = factor.Length;
                    return true;
                }
            }

            transform = p => p;
            scale = 1;
            return false;

            bool matches(Func<Vector2, Vector2> t)
            {
                for (int i = 0; i < circles.Count; i++)
                {
                    if (Vector2.Distance(t(snapshot[i].Position), circles[i].Position) > 1)
                        return false;
                }

                return true;
            }
        }

        private static Vector2 conjugate(Vector2 v) => new Vector2(v.X, -v.Y);

        private static Vector2 multiply(Vector2 a, Vector2 b) => new Vector2(a.X * b.X - a.Y * b.Y, a.X * b.Y + a.Y * b.X);

        private enum ValidationResult
        {
            Unchanged,
            Changed,
            Invalid,
        }

        #endregion

        private void write()
        {
            dirty = false;
            lastWritten = streams.Select(s => s.Serialise()).ToArray();
            slopStreams.Value = lastWritten;
        }

        #region Selection

        private void onSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var h in e.NewItems.OfType<HitObject>())
                {
                    pendingSelected.Add(h);
                    pendingDeselected.Remove(h);
                }
            }

            if (e.OldItems != null)
            {
                foreach (var h in e.OldItems.OfType<HitObject>())
                {
                    pendingDeselected.Add(h);
                    pendingSelected.Remove(h);
                }
            }

            Scheduler.AddOnce(syncSelection);
        }

        /// <summary>
        /// Streams are selected and deselected as a whole.
        /// </summary>
        private void syncSelection()
        {
            var selected = editorBeatmap.SelectedHitObjects;

            var streamsToSelect = pendingSelected.Select(GetStream).OfType<EditorStream>().ToHashSet();

            // circles which were deleted from the beatmap don't deselect the rest of their stream.
            // a stream which was both selected and deselected (e.g. by clicking another of its circles) stays selected.
            var hitObjects = new HashSet<HitObject>(editorBeatmap.HitObjects);
            var toDeselect = pendingDeselected.Where(hitObjects.Contains).Select(GetStream).OfType<EditorStream>().Distinct()
                                              .Where(s => !streamsToSelect.Contains(s))
                                              .SelectMany(s => s.Circles).Where(c => selected.Contains(c)).ToArray();

            var toSelect = streamsToSelect.SelectMany(s => s.Circles).Where(c => !selected.Contains(c)).ToArray();

            pendingSelected.Clear();
            pendingDeselected.Clear();

            foreach (var c in toDeselect)
                selected.Remove(c);

            selected.AddRange(toSelect);

            pendingSelected.Clear();
            pendingDeselected.Clear();

            updateSelectedStream();
        }

        private void updateSelectedStream()
        {
            var selected = editorBeatmap.SelectedHitObjects;

            if (selected.Count == 0)
            {
                SelectedStream.Value = null;
                return;
            }

            var stream = GetStream(selected[0]);

            SelectedStream.Value = stream != null && stream.Circles.Count == selected.Count && stream.Circles.All(selected.Contains) ? stream : null;
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (editorBeatmap.IsNotNull())
            {
                editorBeatmap.TransactionEnding -= onTransactionEnding;
                editorBeatmap.SelectedHitObjects.CollectionChanged -= onSelectionChanged;
            }

            if (changeHandler is TransactionalCommitComponent changeHandlerTransactions)
            {
                changeHandlerTransactions.TransactionEnding -= onTransactionEnding;
                changeHandlerTransactions.SaveStateTriggered -= onTransactionEnding;
            }
        }
    }
}
