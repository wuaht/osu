// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Graphics.UserInterfaceV2.FileSelection
{
    /// <summary>
    /// A small preview of an image file, which is decoded when loading (so it should be loaded asynchronously).
    /// </summary>
    public partial class FileThumbnail : Sprite
    {
        private readonly FileInfo file;
        private readonly int maxSize;

        /// <param name="file">The image file.</param>
        /// <param name="maxSize">The maximum width and height of the thumbnail in pixels.</param>
        public FileThumbnail(FileInfo file, int maxSize)
        {
            this.file = file;
            this.maxSize = maxSize;

            FillMode = FillMode.Fit;
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host)
        {
            try
            {
                using (var stream = file.OpenRead())
                {
                    // decoding at a lower resolution where possible (e.g. for JPEG), as images may be very large.
                    var image = Image.Load<Rgba32>(new DecoderOptions { TargetSize = new Size(maxSize * 2) }, stream);

                    image.Mutate(i => i.Resize(new ResizeOptions
                    {
                        Size = new Size(maxSize),
                        Mode = ResizeMode.Max,
                    }));

                    Texture = host.Renderer.CreateTexture(image.Width, image.Height);
                    // the upload takes ownership of the image.
                    Texture.SetData(new TextureUpload(image));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ImageFormatException or NotSupportedException)
            {
                // the thumbnail is left empty, so that the icon behind it remains visible.
                Logger.Log($"Failed to create a thumbnail of {file.FullName}: {e.Message}");
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            Texture?.Dispose();
        }
    }
}
