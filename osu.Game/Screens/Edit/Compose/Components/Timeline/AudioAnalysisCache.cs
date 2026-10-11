// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Game.Beatmaps;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    /// <summary>
    /// Keeps the analyses of the most recently used audio files, keyed by the stored file rather than the <see cref="WorkingBeatmap"/>,
    /// such that switching between the difficulties of a beatmap set doesn't analyse the same audio again.
    /// </summary>
    public class AudioAnalysisCache<T> where T : class
    {
        /// <summary>
        /// The number of audio files whose analyses are kept.
        /// </summary>
        private const int capacity = 3;

        private readonly Func<Stream, T?> analyse;
        private readonly string description;

        /// <summary>
        /// The analyses, with the most recently used last.
        /// </summary>
        private readonly List<(string key, Task<T?> task)> entries = new List<(string, Task<T?>)>();

        /// <param name="analyse">Analyses an audio file in the background. The stream should be disposed when done.</param>
        /// <param name="description">What is analysed, for logging failures.</param>
        public AudioAnalysisCache(Func<Stream, T?> analyse, string description)
        {
            this.analyse = analyse;
            this.description = description;
        }

        /// <summary>
        /// Retrieves the analysis of the audio of the given beatmap, starting it in the background if it hasn't been performed recently.
        /// Must be called from the update thread, as the audio file is read from the beatmap.
        /// </summary>
        public Task<T?> GetForTrack(WorkingBeatmap beatmap)
        {
            string? key = getAudioKey(beatmap);

            if (key == null)
                return Task.FromResult<T?>(null);

            lock (entries)
            {
                int index = entries.FindIndex(e => e.key == key);

                if (index >= 0)
                {
                    var existing = entries[index];
                    entries.RemoveAt(index);
                    entries.Add(existing);
                    return existing.task;
                }

                var stream = EditorWaveformData.GetAudioStream(beatmap);

                var task = stream == null
                    ? Task.FromResult<T?>(null)
                    : Task.Run(() =>
                    {
                        try
                        {
                            return analyse(stream);
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, $"Failed to analyse the {description} of the track");
                            return null;
                        }
                    });

                entries.Add((key, task));

                while (entries.Count > capacity)
                    entries.RemoveAt(0);

                // failed analyses are retried the next time.
                task.ContinueWith(t =>
                {
                    if (t.GetResultSafely() != null)
                        return;

                    lock (entries)
                        entries.RemoveAll(e => e.task == t);
                });

                return task;
            }
        }

        /// <summary>
        /// The path of the stored audio file of the beatmap, which is based on the hash of its contents.
        /// </summary>
        private static string? getAudioKey(WorkingBeatmap beatmap)
        {
            string? audioFile = beatmap.Metadata?.AudioFile;

            if (string.IsNullOrEmpty(audioFile))
                return null;

            return beatmap.BeatmapSetInfo?.GetPathForFile(audioFile);
        }
    }
}
