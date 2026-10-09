// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Graphics.UserInterfaceV2.FileSelection
{
    /// <summary>
    /// A panel next to the contents of a file or directory selector (like the navigation pane of file explorers),
    /// which offers common directories, the drives and the recently opened directories.
    /// </summary>
    public partial class FileSelectorSidePanel : CompositeDrawable
    {
        public const float WIDTH = 190;

        private const float item_height = 28;

        [Resolved]
        private Bindable<DirectoryInfo?> currentDirectory { get; set; } = null!;

        [Resolved]
        private OsuConfigManager? config { get; set; }

        private readonly IBindable<string> recentDirectoriesSetting = new Bindable<string>(string.Empty);

        private FillFlowContainer recentDirectories = null!;

        public FileSelectorSidePanel()
        {
            Width = WIDTH;
            RelativeSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            var quickAccess = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
            };

            foreach (var (folder, name, icon) in getSpecialFolders())
                quickAccess.Add(new DirectoryItem(folder, name, icon));

            var devices = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Child = new DirectoryItem(null, FileSelectorStrings.ThisPC, FontAwesome.Solid.Desktop),
            };

            foreach (var drive in getDrives())
                devices.Add(new DirectoryItem(drive.RootDirectory, drive.Name, FontAwesome.Solid.Database));

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4,
                },
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ScrollbarVisible = false,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Vertical = 10, Horizontal = 8 },
                        Spacing = new Vector2(0, 4),
                        Children = new Drawable[]
                        {
                            new Header(FileSelectorStrings.QuickAccess),
                            quickAccess,
                            new Header(FileSelectorStrings.ThisPC),
                            devices,
                            recentDirectories = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                            },
                        }
                    }
                },
            };

            if (config != null)
                recentDirectoriesSetting.BindTo(config.GetBindable<string>(OsuSetting.SlopFileSelectorRecentDirectories));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            recentDirectoriesSetting.BindValueChanged(setting =>
            {
                recentDirectories.Clear();

                var directories = RecentDirectories.Get(setting.NewValue);

                if (directories.Count == 0)
                    return;

                recentDirectories.Add(new Header(FileSelectorStrings.RecentlyOpened));

                foreach (var directory in directories)
                {
                    recentDirectories.Add(new DirectoryItem(directory, directory.Name, FontAwesome.Regular.Folder)
                    {
                        TooltipText = directory.FullName,
                    });
                }
            }, true);
        }

        private static IEnumerable<(DirectoryInfo folder, LocalisableString name, IconUsage icon)> getSpecialFolders()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var folders = new (string path, LocalisableString name, IconUsage icon)[]
            {
                (Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), FileSelectorStrings.Desktop, FontAwesome.Solid.Desktop),
                // there is no special folder for downloads, but it is in the home directory by default on all platforms.
                (string.IsNullOrEmpty(home) ? string.Empty : Path.Combine(home, @"Downloads"), FileSelectorStrings.Downloads, FontAwesome.Solid.Download),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), FileSelectorStrings.Documents, FontAwesome.Regular.FileAlt),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), FileSelectorStrings.Pictures, FontAwesome.Regular.Image),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), FileSelectorStrings.Music, FontAwesome.Solid.Music),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), FileSelectorStrings.Videos, FontAwesome.Solid.Film),
            };

            foreach (var (path, name, icon) in folders)
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                    continue;

                yield return (new DirectoryInfo(path), name, icon);
            }
        }

        private static IEnumerable<DriveInfo> getDrives()
        {
            // other platforms have a single root, with many mount points which aren't useful here.
            if (!OperatingSystem.IsWindows())
                return Enumerable.Empty<DriveInfo>();

            try
            {
                return DriveInfo.GetDrives().Where(d => d.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network && d.IsReady).ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Enumerable.Empty<DriveInfo>();
            }
        }

        private partial class Header : OsuSpriteText
        {
            public Header(LocalisableString text)
            {
                Text = text;
                Font = OsuFont.Style.Caption2.With(weight: FontWeight.Bold);
                Margin = new MarginPadding { Left = 8, Top = 6, Bottom = 2 };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                Colour = colourProvider.Content2;
            }
        }

        private partial class DirectoryItem : OsuClickableContainer
        {
            /// <summary>
            /// The directory, or <c>null</c> for the list of drives.
            /// </summary>
            private readonly DirectoryInfo? directory;

            private readonly LocalisableString name;
            private readonly IconUsage icon;

            [Resolved]
            private Bindable<DirectoryInfo?> currentDirectory { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private Box background = null!;

            public DirectoryItem(DirectoryInfo? directory, LocalisableString name, IconUsage icon)
            {
                this.directory = directory;
                this.name = name;
                this.icon = icon;

                RelativeSizeAxes = Axes.X;
                Height = item_height;
                Masking = true;
                CornerRadius = 5;

                Action = () => currentDirectory.Value = directory;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background2,
                        Alpha = 0,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(14),
                        Margin = new MarginPadding { Left = 8 },
                        Icon = icon,
                        Colour = colourProvider.Light3,
                    },
                    new TruncatingSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        Padding = new MarginPadding { Left = 30, Right = 8 },
                        Text = name,
                        Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                        Colour = colourProvider.Content1,
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                currentDirectory.BindValueChanged(_ => updateState(), true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateState();
                base.OnHoverLost(e);
            }

            private void updateState()
            {
                bool selected = isSameDirectory(currentDirectory.Value, directory);
                background.FadeTo(selected ? 1 : IsHovered ? 0.5f : 0, 100, Easing.OutQuint);
            }

            private static bool isSameDirectory(DirectoryInfo? a, DirectoryInfo? b)
            {
                if (a == null || b == null)
                    return a == b;

                return string.Equals(Path.TrimEndingDirectorySeparator(a.FullName), Path.TrimEndingDirectorySeparator(b.FullName),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            }
        }
    }
}
