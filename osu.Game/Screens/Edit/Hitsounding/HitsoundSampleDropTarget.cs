// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.OSD;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Adds sample files dragged onto a lane as a custom sample of the lane, highlighting the lane while dragging over it.
    /// Dropping onto a lane adds the sample as a new custom sample set, dropping onto a sub-lane replaces its sample.
    /// </summary>
    public partial class HitsoundSampleDropTarget : CompositeDrawable, ICanAcceptFiles
    {
        public IEnumerable<string> HandledExtensions => SupportedExtensions.AUDIO_EXTENSIONS;

        [Resolved]
        private HitsoundEditor hitsoundEditor { get; set; } = null!;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> working { get; set; } = null!;

        [Resolved]
        private OnScreenDisplay? onScreenDisplay { get; set; }

        private Container ring = null!;
        private OsuSpriteText label = null!;

        private readonly object dropLock = new object();

        /// <summary>
        /// The last position of the drag over the window, in screen space.
        /// </summary>
        private Vector2? dragPosition;

        /// <summary>
        /// The positions where files were dropped, until they are imported.
        /// </summary>
        private readonly Dictionary<string, Vector2> dropPositions = new Dictionary<string, Vector2>();

        public HitsoundSampleDropTarget()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChild = ring = new Container
            {
                RelativeSizeAxes = Axes.X,
                Masking = true,
                CornerRadius = 4,
                BorderThickness = 2,
                BorderColour = colourProvider.Highlight1,
                Alpha = 0,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Highlight1,
                        Alpha = 0.15f,
                    },
                    label = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -10,
                        Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                        Colour = colourProvider.Highlight1,
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (host.Window != null)
            {
                host.Window.DragOver += onDragOver;
                host.Window.DragDrop += onDragDrop;
                host.Window.DragEnded += onDragEnded;
            }

            game.RegisterImportHandler(this);
        }

        // the window's events arrive on the input thread.
        private void onDragOver(Vector2 position)
        {
            lock (dropLock)
                dragPosition = position;

            Schedule(() => updateRing(position));
        }

        private void onDragDrop(string path)
        {
            if (!HandledExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                return;

            lock (dropLock)
            {
                if (dragPosition is Vector2 position)
                    dropPositions[path] = position;
            }
        }

        private void onDragEnded()
        {
            lock (dropLock)
                dragPosition = null;

            Schedule(() => ring.FadeOut(100, Easing.OutQuint));
        }

        private void updateRing(Vector2 screenSpacePosition)
        {
            var lane = getLaneAt(screenSpacePosition, out int index);

            if (lane == null)
            {
                ring.FadeOut(100, Easing.OutQuint);
                return;
            }

            float laneHeight = DrawHeight / hitsoundEditor.VisibleLanes.Count;

            ring.Y = index * laneHeight;
            ring.Height = laneHeight;
            ring.FadeIn(100, Easing.OutQuint);

            int customIndex = getTargetCustomIndex(lane.Value);
            label.Text = lane.Value.CustomIndex == customIndex
                ? SlopHitsoundEditorStrings.DropSampleReplace(HitsoundSoundGuide.GetLaneName(lane.Value.Parent), HitsoundLaneHeaders.GetCustomIndexName(customIndex))
                : SlopHitsoundEditorStrings.DropSampleAdd(HitsoundSoundGuide.GetLaneName(lane.Value.Parent), HitsoundLaneHeaders.GetCustomIndexName(customIndex));
        }

        /// <summary>
        /// The visible lane at a screen space position, or null if there is none (or the lanes aren't shown).
        /// </summary>
        private HitsoundLane? getLaneAt(Vector2 screenSpacePosition, out int index)
        {
            index = -1;

            var lanes = hitsoundEditor.VisibleLanes;

            if (lanes.Count == 0 || !this.IsPresentInHierarchy() || !ReceivePositionalInputAt(screenSpacePosition))
                return null;

            index = Math.Clamp((int)Math.Floor(ToLocalSpace(screenSpacePosition).Y / (DrawHeight / lanes.Count)), 0, lanes.Count - 1);
            return lanes[index];
        }

        /// <summary>
        /// The custom sample index a sample dropped onto a lane is added as: the one of a sub-lane (except for the skin's samples),
        /// or otherwise the first one which isn't used for the lane yet, such that the sample doesn't change how any hitsounds sound.
        /// </summary>
        private int getTargetCustomIndex(HitsoundLane lane)
        {
            if (lane.CustomIndex is int customIndex && customIndex > 0)
                return customIndex;

            var taken = hitsoundEditor.GetSubLaneIndices(lane);

            int index = 2;

            while (taken.Contains(index))
                index++;

            return index;
        }

        Task ICanAcceptFiles.Import(params string[] paths)
        {
            var drops = new List<(string path, Vector2 position)>();

            lock (dropLock)
            {
                foreach (string path in paths)
                {
                    if (dropPositions.Remove(path, out var position))
                        drops.Add((path, position));
                }
            }

            if (drops.Count > 0)
                Schedule(() => addSample(drops[0].path, drops[0].position));

            return Task.CompletedTask;
        }

        Task ICanAcceptFiles.Import(ImportTask[] tasks, ImportParameters parameters) => throw new NotImplementedException();

        private void addSample(string path, Vector2 screenSpacePosition)
        {
            if (getLaneAt(screenSpacePosition, out _) is not HitsoundLane lane)
                return;

            int customIndex = getTargetCustomIndex(lane);
            bool replacing = lane.CustomIndex == customIndex;

            // e.g. "soft-hitwhistle.wav" for the beatmap's default samples, and "soft-hitwhistle2.wav" for custom index 2.
            string name = customIndex >= 2 ? $@"{lane.Bank}-{lane.Sample}{customIndex}" : $@"{lane.Bank}-{lane.Sample}";
            string filename = name + Path.GetExtension(path).ToLowerInvariant();

            var set = working.Value.BeatmapSetInfo;

            try
            {
                // a sample with another extension (or casing) would otherwise be played instead.
                foreach (var existing in set.Files.Where(f => !f.Filename.Contains('/') && string.Equals(Path.GetFileNameWithoutExtension(f.Filename), name, StringComparison.OrdinalIgnoreCase)).ToList())
                    beatmaps.DeleteFile(set, existing);

                using (var stream = File.OpenRead(path))
                    beatmaps.AddFile(set, stream, filename);
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"Couldn't add ""{Path.GetFileName(path)}"" as a sample");
                return;
            }

            Logger.Log($@"Added ""{Path.GetFileName(path)}"" as ""{filename}""");

            string laneName = HitsoundSoundGuide.GetLaneName(lane.Parent);
            var customIndexName = HitsoundLaneHeaders.GetCustomIndexName(customIndex);

            onScreenDisplay?.Display(new SampleToast(replacing
                ? SlopHitsoundEditorStrings.SampleReplaced(laneName, customIndexName)
                : SlopHitsoundEditorStrings.SampleAdded(laneName, customIndexName), Path.GetFileName(path)));

            // the sub-lane of the sample appears once the beatmap skin has picked up the file.
            if (!hitsoundEditor.ExpandedLanes.Contains(lane.Parent))
                hitsoundEditor.ExpandedLanes.Add(lane.Parent);
        }

        private partial class SampleToast : Toast
        {
            public SampleToast(LocalisableString value, string sampleFilename)
                : base(SlopHitsoundEditorStrings.Hitsounds, value)
            {
                ExtraText = sampleFilename;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (host.IsNotNull() && host.Window != null)
            {
                host.Window.DragOver -= onDragOver;
                host.Window.DragDrop -= onDragDrop;
                host.Window.DragEnded -= onDragEnded;
            }

            if (game.IsNotNull())
                game.UnregisterImportHandler(this);
        }
    }
}
