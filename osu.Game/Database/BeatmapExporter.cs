// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Text;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;

namespace osu.Game.Database
{
    /// <summary>
    /// Exporter for beatmap archives.
    /// This is not for legacy purposes and works for lazer only.
    /// </summary>
    public class BeatmapExporter : LegacyArchiveExporter<BeatmapSetInfo>
    {
        public BeatmapExporter(Storage storage)
            : base(storage)
        {
        }

        protected override bool UseFixedEncoding => false;

        protected override string FileExtension => @".olz";

        protected override Stream? GetFileContents(BeatmapSetInfo model, INamedFileUsage file)
        {
            var stream = base.GetFileContents(model, file);

            if (stream == null || model.Beatmaps.All(b => b.Hash != file.File.Hash))
                return stream;

            // editor-only streams are exported as the regular hit circles they consist of.
            string contents;
            using (var reader = new StreamReader(stream))
                contents = reader.ReadToEnd();

            return new MemoryStream(Encoding.UTF8.GetBytes(RemoveSlopStreamsSection(contents)));
        }

        /// <summary>
        /// Removes the editor-only <c>[SlopStreams]</c> section from the contents of a beatmap file.
        /// </summary>
        public static string RemoveSlopStreamsSection(string contents)
        {
            string header = $"[{LegacyDecoder<Beatmap>.Section.SlopStreams}]";
            int start = contents.IndexOf(header, StringComparison.Ordinal);

            if (start < 0)
                return contents;

            int end = contents.IndexOf("\n[", start + header.Length, StringComparison.Ordinal);

            return end < 0 ? contents[..start].TrimEnd() + Environment.NewLine : contents[..start] + contents[(end + 1)..];
        }
    }
}
