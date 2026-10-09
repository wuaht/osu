// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Graphics.UserInterfaceV2.FileSelection;
using osu.Game.Overlays;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneDirectorySelectorPathInput : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        private string tempDirectory = null!;
        private string subDirectory = null!;
        private string file = null!;

        private OsuDirectorySelector selector = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create directories", () =>
            {
                tempDirectory = Path.Combine(Path.GetTempPath(), $"path-input-{Guid.NewGuid():N}");
                subDirectory = Path.Combine(tempDirectory, "sub folder");
                file = Path.Combine(subDirectory, "file.txt");

                Directory.CreateDirectory(subDirectory);
                File.WriteAllText(file, string.Empty);
            });

            AddStep("create selector", () => Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Child = selector = new OsuDirectorySelector(tempDirectory) { RelativeSizeAxes = Axes.Both },
            });

            AddUntilStep("at temp directory", () => selector.CurrentPath.Value?.FullName, () => Is.EqualTo(new DirectoryInfo(tempDirectory).FullName));
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("delete directories", () =>
            {
                try
                {
                    Directory.Delete(tempDirectory, true);
                }
                catch
                {
                    // not important.
                }
            });
        }

        [Test]
        public void TestEnterPath()
        {
            startEditing();

            AddAssert("current path entered", () => textBox.Text, () => Is.EqualTo(new DirectoryInfo(tempDirectory).FullName));
            AddAssert("current path selected", () => textBox.SelectedText, () => Is.EqualTo(textBox.Text));
            AddAssert("text box fits into the breadcrumbs", () => textBox.DrawHeight, () => Is.LessThan(OsuDirectorySelectorBreadcrumbDisplay.HEIGHT));

            AddStep("enter quoted path", () =>
            {
                textBox.Text = $"\"{subDirectory}\"";
                InputManager.Key(Key.Enter);
            });

            AddAssert("at sub directory", () => selector.CurrentPath.Value?.FullName, () => Is.EqualTo(new DirectoryInfo(subDirectory).FullName));
            AddUntilStep("breadcrumbs displayed", () => !textBox.HasFocus && textBox.Parent!.Alpha == 0);
        }

        [Test]
        public void TestInvalidPathKeepsEditing()
        {
            startEditing();

            AddStep("enter missing path", () =>
            {
                textBox.Text = Path.Combine(tempDirectory, "missing");
                InputManager.Key(Key.Enter);
            });

            AddAssert("still at temp directory", () => selector.CurrentPath.Value?.FullName, () => Is.EqualTo(new DirectoryInfo(tempDirectory).FullName));
            AddAssert("still editing", () => textBox.HasFocus);

            AddStep("press escape", () => InputManager.Key(Key.Escape));
            AddUntilStep("breadcrumbs displayed", () => textBox.Parent!.Alpha == 0);
        }

        [Test]
        public void TestResolveDirectory()
        {
            AddAssert("directory", () => OsuDirectorySelectorBreadcrumbDisplay.ResolveDirectory(subDirectory)?.FullName, () => Is.EqualTo(new DirectoryInfo(subDirectory).FullName));
            AddAssert("quoted with whitespace", () => OsuDirectorySelectorBreadcrumbDisplay.ResolveDirectory($"  \"{subDirectory}\" ")?.FullName,
                () => Is.EqualTo(new DirectoryInfo(subDirectory).FullName));
            AddAssert("file leads to its directory", () => OsuDirectorySelectorBreadcrumbDisplay.ResolveDirectory(file)?.FullName, () => Is.EqualTo(new DirectoryInfo(subDirectory).FullName));
            AddAssert("environment variable", () => OsuDirectorySelectorBreadcrumbDisplay.ResolveDirectory("%TEMP%")?.FullName,
                () => Is.EqualTo(new DirectoryInfo(Environment.ExpandEnvironmentVariables("%TEMP%")).FullName));
            AddAssert("missing", () => OsuDirectorySelectorBreadcrumbDisplay.ResolveDirectory(Path.Combine(tempDirectory, "missing")), () => Is.Null);
            AddAssert("empty", () => OsuDirectorySelectorBreadcrumbDisplay.ResolveDirectory("  "), () => Is.Null);
            AddAssert("invalid", () => OsuDirectorySelectorBreadcrumbDisplay.ResolveDirectory("\0"), () => Is.Null);
        }

        private OsuTextBox textBox => selector.ChildrenOfType<OsuDirectorySelectorBreadcrumbDisplay>().Single().ChildrenOfType<OsuTextBox>().Single();

        private void startEditing()
        {
            AddStep("click edit button", () =>
            {
                InputManager.MoveMouseTo(selector.ChildrenOfType<OsuDirectorySelectorBreadcrumbDisplay>().Single().ChildrenOfType<IconButton>().Single());
                InputManager.Click(MouseButton.Left);
            });

            AddUntilStep("text box focused", () => textBox.HasFocus);
        }
    }
}
