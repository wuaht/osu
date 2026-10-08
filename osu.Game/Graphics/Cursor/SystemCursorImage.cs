// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using osu.Framework.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// Provides the image of the system's arrow cursor, and transformed variants of it which are used to animate the system cursor.
    /// </summary>
    public static class SystemCursorImage
    {
        /// <summary>
        /// Loads the image of the system's arrow cursor.
        /// On Windows, this is the arrow of the user's current pointer scheme. Elsewhere, a standard arrow is used.
        /// </summary>
        /// <param name="hotspot">The click point of the cursor within the returned image.</param>
        public static Image<Rgba32> Load(out Point hotspot)
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var image = loadWindowsArrow(out hotspot);
                    if (image != null)
                        return image;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to load the Windows arrow cursor, falling back to a standard arrow.");
                }
            }

            return createStandardArrow(out hotspot);
        }

        /// <summary>
        /// Renders a transformed variant of a cursor image.
        /// </summary>
        /// <param name="source">The cursor image.</param>
        /// <param name="hotspot">The click point of the cursor within <paramref name="source"/>, which is the centre of the transformations.</param>
        /// <param name="rotation">The clockwise rotation, in degrees.</param>
        /// <param name="scale">The scale.</param>
        /// <param name="highlight">The strength of the highlight colour added to the cursor, from 0 to 1.</param>
        /// <param name="highlightColour">The highlight colour.</param>
        /// <param name="renderedHotspot">The click point of the cursor within the returned image.</param>
        public static Image<Rgba32> Render(Image<Rgba32> source, Point hotspot, float rotation, float scale, float highlight, Vector3 highlightColour, out Point renderedHotspot)
        {
            // Transform around the centre of the hotspot pixel, such that untransformed images stay pixel-aligned.
            Matrix3x2 transform = Matrix3x2.CreateTranslation(-(hotspot.X + 0.5f), -(hotspot.Y + 0.5f))
                                  * Matrix3x2.CreateScale(scale)
                                  * Matrix3x2.CreateRotation(float.DegreesToRadians(rotation));

            // Fit the output to the bounds of the transformed image, with a pixel of margin for the filtering of transformed images.
            Vector2 min = new Vector2(float.MaxValue);
            Vector2 max = new Vector2(float.MinValue);

            foreach (var corner in new[] { new Vector2(0), new Vector2(source.Width, 0), new Vector2(0, source.Height), new Vector2(source.Width, source.Height) })
            {
                Vector2 transformed = Vector2.Transform(corner, transform);
                min = Vector2.Min(min, transformed);
                max = Vector2.Max(max, transformed);
            }

            int margin = rotation == 0 && scale == 1 ? 0 : 1;

            // The hotspot ends up at the centre of this pixel.
            renderedHotspot = new Point((int)MathF.Ceiling(-min.X - 0.5f) + margin, (int)MathF.Ceiling(-min.Y - 0.5f) + margin);

            var size = new Size(
                (int)MathF.Ceiling(max.X + renderedHotspot.X + 0.5f) + margin,
                (int)MathF.Ceiling(max.Y + renderedHotspot.Y + 0.5f) + margin);

            transform *= Matrix3x2.CreateTranslation(renderedHotspot.X + 0.5f, renderedHotspot.Y + 0.5f);

            var result = source.Clone(c => c.Transform(new Rectangle(0, 0, source.Width, source.Height), transform, size, KnownResamplers.Bicubic));

            if (highlight > 0)
            {
                Vector3 added = highlightColour * highlight;

                result.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);

                        for (int x = 0; x < row.Length; x++)
                        {
                            Vector4 pixel = row[x].ToVector4();
                            pixel = new Vector4(Vector3.Min(new Vector3(pixel.X, pixel.Y, pixel.Z) + added, Vector3.One), pixel.W);
                            row[x].FromVector4(pixel);
                        }
                    }
                });
            }

            return result;
        }

        /// <summary>
        /// The standard arrow cursor. 'X' is the outline, '.' the fill.
        /// </summary>
        private static readonly string[] standard_arrow =
        {
            "X",
            "XX",
            "X.X",
            "X..X",
            "X...X",
            "X....X",
            "X.....X",
            "X......X",
            "X.......X",
            "X........X",
            "X.........X",
            "X..........X",
            "X......XXXXXX",
            "X...X..X",
            "X..XX..X",
            "X.X  X..X",
            "XX   X..X",
            "X     X..X",
            "      X..X",
            "       X..X",
            "       XXX",
        };

        private static Image<Rgba32> createStandardArrow(out Point hotspot)
        {
            const int scale = 2;

            hotspot = new Point(0, 0);

            var image = new Image<Rgba32>(13 * scale, standard_arrow.Length * scale);

            for (int y = 0; y < image.Height; y++)
            {
                string row = standard_arrow[y / scale];

                for (int x = 0; x < image.Width; x++)
                {
                    char c = x / scale < row.Length ? row[x / scale] : ' ';

                    image[x, y] = c switch
                    {
                        'X' => new Rgba32(0, 0, 0, 255),
                        '.' => new Rgba32(255, 255, 255, 255),
                        _ => new Rgba32(0, 0, 0, 0),
                    };
                }
            }

            return image;
        }

        #region Windows

        [SupportedOSPlatform("windows")]
        private static Image<Rgba32>? loadWindowsArrow(out Point hotspot)
        {
            hotspot = default;

            // The shared arrow cursor, which reflects the user's pointer scheme and size.
            IntPtr cursor = LoadCursor(IntPtr.Zero, new IntPtr(idc_arrow));
            if (cursor == IntPtr.Zero || !GetIconInfo(cursor, out var iconInfo))
                return null;

            try
            {
                hotspot = new Point(iconInfo.xHotspot, iconInfo.yHotspot);

                if (GetObject(iconInfo.hbmMask, Marshal.SizeOf<BITMAP>(), out var maskBitmap) == 0)
                    return null;

                int width = maskBitmap.bmWidth;

                if (iconInfo.hbmColor != IntPtr.Zero)
                {
                    if (GetObject(iconInfo.hbmColor, Marshal.SizeOf<BITMAP>(), out var colourBitmap) == 0)
                        return null;

                    int height = colourBitmap.bmHeight;

                    byte[]? colour = getBitmapPixels(iconInfo.hbmColor, width, height);
                    byte[]? mask = getBitmapPixels(iconInfo.hbmMask, width, height);

                    if (colour == null || mask == null)
                        return null;

                    // Colour cursors without an alpha channel use the mask for transparency.
                    bool hasAlpha = false;
                    for (int i = 3; i < colour.Length; i += 4)
                        hasAlpha |= colour[i] != 0;

                    var image = new Image<Rgba32>(width, height);

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int i = (y * width + x) * 4;
                            bool transparent = mask[i] != 0;
                            byte alpha = hasAlpha ? colour[i + 3] : (byte)(transparent ? 0 : 255);

                            image[x, y] = new Rgba32(colour[i + 2], colour[i + 1], colour[i], alpha);
                        }
                    }

                    return image;
                }
                else
                {
                    // Monochrome cursors consist of an AND mask (top half) and an XOR mask (bottom half).
                    int height = maskBitmap.bmHeight / 2;

                    byte[]? mask = getBitmapPixels(iconInfo.hbmMask, width, height * 2);
                    if (mask == null)
                        return null;

                    var image = new Image<Rgba32>(width, height);

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            bool and = mask[(y * width + x) * 4] != 0;
                            bool xor = mask[((y + height) * width + x) * 4] != 0;

                            image[x, y] = and && !xor
                                ? new Rgba32(0, 0, 0, 0)
                                // Inverting pixels (AND and XOR) can't be represented, so they are drawn black.
                                : xor && !and
                                    ? new Rgba32(255, 255, 255, 255)
                                    : new Rgba32(0, 0, 0, 255);
                        }
                    }

                    return image;
                }
            }
            finally
            {
                if (iconInfo.hbmMask != IntPtr.Zero)
                    DeleteObject(iconInfo.hbmMask);
                if (iconInfo.hbmColor != IntPtr.Zero)
                    DeleteObject(iconInfo.hbmColor);
            }
        }

        /// <summary>
        /// Retrieves the pixels of a bitmap as top-down 32-bit BGRA.
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static byte[]? getBitmapPixels(IntPtr bitmap, int width, int height)
        {
            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                // Negative for top-down rows.
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = bi_rgb,
            };

            byte[] pixels = new byte[width * height * 4];

            IntPtr dc = GetDC(IntPtr.Zero);

            try
            {
                return GetDIBits(dc, bitmap, 0, (uint)height, pixels, ref header, dib_rgb_colors) == height ? pixels : null;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, dc);
            }
        }

        private const int idc_arrow = 32512;
        private const uint bi_rgb = 0;
        private const uint dib_rgb_colors = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool fIcon;

            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [DllImport("user32.dll", EntryPoint = "LoadCursorW")]
        private static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
        private static extern int GetObject(IntPtr h, int c, out BITMAP pv);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, [Out] byte[] lpvBits, ref BITMAPINFOHEADER lpbmi, uint usage);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr ho);

        #endregion
    }
}
