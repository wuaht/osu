// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Graphics.UserInterfaceV2.FileSelection
{
    internal partial class OsuDirectorySelectorBreadcrumbDisplay : DirectorySelectorBreadcrumbDisplay
    {
        public const float HEIGHT = 45;
        public const float HORIZONTAL_PADDING = 20;

        [Resolved]
        private Bindable<DirectoryInfo> currentDirectory { get; set; } = null!;

        private FillFlowContainer breadcrumbs = null!;
        private Container pathInput = null!;
        private PathTextBox pathTextBox = null!;

        /// <summary>
        /// Whether a path is being entered instead of the breadcrumbs being displayed.
        /// </summary>
        private bool editingPath;

        protected override Drawable CreateCaption() => Empty().With(d =>
        {
            d.Origin = Anchor.CentreLeft;
            d.Anchor = Anchor.CentreLeft;
            d.Alpha = 0;
        });

        protected override DirectorySelectorDirectory CreateRootDirectoryItem() => new OsuBreadcrumbDisplayDevice();

        protected override DirectorySelectorDirectory CreateDirectoryItem(DirectoryInfo directory, LocalisableString? displayName = null) => new OsuBreadcrumbDisplayDirectory(directory, displayName);

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            breadcrumbs = (FillFlowContainer)InternalChild;
            breadcrumbs.Padding = new MarginPadding
            {
                Left = HORIZONTAL_PADDING,
                // leaves space for the button which allows entering a path.
                Right = HORIZONTAL_PADDING + IconButton.DEFAULT_BUTTON_SIZE,
                Vertical = 10,
            };

            // keeps the height of this display while a path is entered.
            breadcrumbs.AlwaysPresent = true;

            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = colourProvider.Background4,
                Depth = 1,
            });

            // a fixed height, as the height of this display depends on the breadcrumbs. centred in line with the button.
            AddInternal(pathInput = new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                RelativeSizeAxes = Axes.X,
                Height = HEIGHT,
                Padding = new MarginPadding { Left = HORIZONTAL_PADDING, Right = HORIZONTAL_PADDING + IconButton.DEFAULT_BUTTON_SIZE, Vertical = 7 },
                Alpha = 0,
                Child = pathTextBox = new PathTextBox
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    // text boxes have an absolute height by default, which would otherwise be used as the relative height.
                    Size = Vector2.One,
                    PlaceholderText = FileSelectorStrings.EnterPathPlaceholder,
                    ReleaseFocusOnCommit = false,
                    LostFocus = endEditingPath,
                },
            });

            pathTextBox.OnCommit += (_, _) => commitPath();

            AddInternal(new IconButton
            {
                // vertically centred like the toggle for hidden files next to it, also when the breadcrumbs span multiple lines.
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Margin = new MarginPadding { Right = HORIZONTAL_PADDING / 2 },
                Icon = FontAwesome.Solid.PencilAlt,
                TooltipText = FileSelectorStrings.EnterPath,
                Action = () =>
                {
                    if (editingPath)
                        endEditingPath();
                    else
                        startEditingPath();
                },
            });
        }

        // like file explorers, clicking next to the breadcrumbs allows entering a path.
        protected override bool OnClick(ClickEvent e)
        {
            startEditingPath();
            return true;
        }

        private void startEditingPath()
        {
            if (editingPath)
                return;

            editingPath = true;

            pathTextBox.Text = currentDirectory.Value?.FullName ?? string.Empty;

            breadcrumbs.Alpha = 0;
            pathInput.Alpha = 1;

            GetContainingFocusManager()?.ChangeFocus(pathTextBox);

            // selected so that a pasted path replaces the current one.
            // the text box only applies the text once it updated (it doesn't while hidden), which would reset the selection.
            ScheduleAfterChildren(() => pathTextBox.SelectAll());
        }

        private void endEditingPath()
        {
            if (!editingPath)
                return;

            editingPath = false;

            breadcrumbs.Alpha = 1;
            pathInput.Alpha = 0;

            if (pathTextBox.HasFocus)
                GetContainingFocusManager()?.ChangeFocus(null);
        }

        private void commitPath()
        {
            var directory = ResolveDirectory(pathTextBox.Text);

            if (directory == null)
            {
                pathTextBox.FlashColour(Colour4.Red, 300);
                return;
            }

            currentDirectory.Value = directory;
            endEditingPath();
        }

        /// <summary>
        /// Returns the directory which an entered path leads to: the directory itself, or the directory of a file.
        /// </summary>
        /// <param name="path">The entered path, which may be quoted (as copied by file explorers) and contain environment variables.</param>
        /// <returns>The directory, or <c>null</c> if the path doesn't lead to an existing directory.</returns>
        public static DirectoryInfo? ResolveDirectory(string path)
        {
            path = path.Trim().Trim('"').Trim();

            if (string.IsNullOrEmpty(path))
                return null;

            path = Environment.ExpandEnvironmentVariables(path);

            if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith(@"~\", StringComparison.Ordinal))
                path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];

            try
            {
                path = Path.GetFullPath(path);

                if (Directory.Exists(path))
                    return new DirectoryInfo(path);

                if (File.Exists(path))
                    return new FileInfo(path).Directory;
            }
            catch (Exception e) when (e is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // not a valid path.
            }

            return null;
        }

        private partial class PathTextBox : OsuTextBox
        {
            public Action? LostFocus;

            protected override void OnFocusLost(FocusLostEvent e)
            {
                base.OnFocusLost(e);
                LostFocus?.Invoke();
            }
        }

        private partial class OsuBreadcrumbDisplayDevice : OsuBreadcrumbDisplayDirectory
        {
            protected override IconUsage? Icon => null;

            public OsuBreadcrumbDisplayDevice()
                : base(null, UserInterfaceStrings.Device)
            {
            }
        }

        private partial class OsuBreadcrumbDisplayDirectory : DirectorySelectorDirectory
        {
            public OsuBreadcrumbDisplayDirectory(DirectoryInfo? directory, LocalisableString? displayName = null)
                : base(directory, displayName)
            {
            }

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            [BackgroundDependencyLoader]
            private void load()
            {
                Anchor = Anchor.CentreLeft;
                Origin = Anchor.CentreLeft;

                Flow.AutoSizeAxes = Axes.X;
                Flow.Height = 25;
                Flow.Margin = new MarginPadding { Horizontal = 10, };

                AddInternal(new BackgroundLayer(0.5f)
                {
                    Depth = 1
                });

                Flow.Add(new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Icon = FontAwesome.Solid.ChevronRight,
                    Size = new Vector2(FONT_SIZE / 2),
                    Margin = new MarginPadding { Left = 5, },
                });
                Flow.Colour = colourProvider.Light3;
            }

            protected override SpriteText CreateSpriteText() => new OsuSpriteText().With(t => t.Font = OsuFont.Default.With(weight: FontWeight.SemiBold));

            protected override IconUsage? Icon => Directory.Name.Contains(Path.DirectorySeparatorChar) ? FontAwesome.Solid.Database : null;
        }
    }
}
