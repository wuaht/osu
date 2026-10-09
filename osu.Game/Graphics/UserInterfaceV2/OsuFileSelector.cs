// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2.FileSelection;
using osu.Game.Overlays;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Graphics.UserInterfaceV2
{
    public partial class OsuFileSelector : FileSelector
    {
        private Box hiddenToggleBackground = null!;

        public OsuFileSelector(string? initialPath = null, string[]? validFileExtensions = null)
            : base(initialPath, validFileExtensions)
        {
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = colourProvider.Background5,
                Depth = float.MaxValue,
            });

            hiddenToggleBackground.Colour = colourProvider.Background4;

            // like the navigation pane of file explorers.
            TopLevelContent.Padding = new MarginPadding { Left = FileSelectorSidePanel.WIDTH };
            AddInternal(new FileSelectorSidePanel());
        }

        [Resolved]
        private OsuConfigManager? config { get; set; }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // only files selected by the user, not the initially selected one.
            CurrentFile.BindValueChanged(file =>
            {
                if (config != null && file.NewValue?.Directory is DirectoryInfo directory)
                    RecentDirectories.Add(config, directory);
            });
        }

        protected override ScrollContainer<Drawable> CreateScrollContainer() => new OsuScrollContainer
        {
            Padding = new MarginPadding
            {
                Horizontal = 20,
                Vertical = 15,
            }
        };

        protected override DirectorySelectorBreadcrumbDisplay CreateBreadcrumb() => new OsuDirectorySelectorBreadcrumbDisplay();

        protected override Drawable CreateHiddenToggleButton() => new Container
        {
            RelativeSizeAxes = Axes.Y,
            AutoSizeAxes = Axes.X,
            Children = new Drawable[]
            {
                hiddenToggleBackground = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new HiddenFilesToggleCheckbox
                {
                    Current = { BindTarget = ShowHiddenItems },
                },
            }
        };

        protected override DirectorySelectorDirectory CreateParentDirectoryItem(DirectoryInfo directory) => new OsuDirectorySelectorParentDirectory(directory);

        protected override DirectorySelectorDirectory CreateDirectoryItem(DirectoryInfo directory, LocalisableString? displayName = null) => new OsuDirectorySelectorDirectory(directory, displayName);

        protected override DirectoryListingFile CreateFileItem(FileInfo file) => new OsuDirectoryListingFile(file);

        protected override void NotifySelectionError() => this.FlashColour(Colour4.Red, 300);

        protected partial class OsuDirectoryListingFile : DirectoryListingFile
        {
            /// <summary>
            /// The size of the previews of images.
            /// </summary>
            private const int thumbnail_size = 40;

            private readonly bool isImage;

            private Container thumbnail = null!;
            private SpriteIcon fallbackIcon = null!;

            public OsuDirectoryListingFile(FileInfo file)
                : base(file)
            {
                isImage = SupportedExtensions.IMAGE_EXTENSIONS.Contains(file.Extension.ToLowerInvariant());
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                Flow.AutoSizeAxes = Axes.X;
                Flow.Height = isImage ? thumbnail_size : OsuDirectorySelector.ITEM_HEIGHT;

                if (isImage)
                {
                    // a preview instead of the icon, which is only loaded once it is displayed (as directories may contain many images).
                    Flow.Insert(-1, thumbnail = new Container
                    {
                        Size = new Vector2(thumbnail_size),
                        Margin = new MarginPadding { Right = 5 },
                        Children = new Drawable[]
                        {
                            fallbackIcon = new SpriteIcon
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Size = new Vector2(FONT_SIZE),
                                Icon = FontAwesome.Regular.FileImage,
                            },
                            new DelayedLoadWrapper(() => new FileThumbnail(File, thumbnail_size)
                            {
                                RelativeSizeAxes = Axes.Both,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                            }, 0)
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                        }
                    });

                    foreach (var child in Flow)
                    {
                        child.Anchor = Anchor.CentreLeft;
                        child.Origin = Anchor.CentreLeft;
                    }
                }

                AddInternal(new BackgroundLayer());

                if (isImage)
                {
                    // the preview keeps its colours.
                    foreach (var child in Flow.Where(c => c != thumbnail))
                        child.Colour = colourProvider.Light3;

                    fallbackIcon.Colour = colourProvider.Light3;
                }
                else
                    Colour = colourProvider.Light3;
            }

            protected override IconUsage? Icon
            {
                get
                {
                    // images have a preview instead.
                    if (isImage)
                        return null;

                    string extension = File.Extension.ToLowerInvariant();

                    if (SupportedExtensions.VIDEO_EXTENSIONS.Contains(extension))
                        return FontAwesome.Regular.FileVideo;

                    if (SupportedExtensions.AUDIO_EXTENSIONS.Contains(extension))
                        return FontAwesome.Regular.FileAudio;

                    if (SupportedExtensions.IMAGE_EXTENSIONS.Contains(extension))
                        return FontAwesome.Regular.FileImage;

                    return FontAwesome.Regular.File;
                }
            }

            protected override SpriteText CreateSpriteText() => new OsuSpriteText().With(t => t.Font = OsuFont.Default.With(weight: FontWeight.SemiBold));
        }
    }
}
