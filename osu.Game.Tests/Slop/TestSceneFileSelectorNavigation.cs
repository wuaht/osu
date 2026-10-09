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
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Graphics.UserInterfaceV2.FileSelection;
using osu.Game.Overlays;
using osu.Game.Tests.Visual;
using osuTK.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneFileSelectorNavigation : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private string tempDirectory = null!;
        private string recentDirectory = null!;

        private OsuFileSelector selector = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create directories", () =>
            {
                tempDirectory = Path.Combine(Path.GetTempPath(), $"file-selector-{Guid.NewGuid():N}");
                recentDirectory = Path.Combine(tempDirectory, "recent");

                Directory.CreateDirectory(recentDirectory);

                using (var image = new Image<Rgba32>(100, 50, new Rgba32(255, 0, 0)))
                    image.SaveAsPng(Path.Combine(tempDirectory, "image.png"));

                File.WriteAllText(Path.Combine(tempDirectory, "text.txt"), string.Empty);

                config.SetValue(OsuSetting.SlopFileSelectorRecentDirectories, recentDirectory);
            });

            AddStep("create selector", () => Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Child = selector = new OsuFileSelector(tempDirectory) { RelativeSizeAxes = Axes.Both },
            });

            AddUntilStep("at temp directory", () => selector.CurrentPath.Value?.FullName, () => Is.EqualTo(new DirectoryInfo(tempDirectory).FullName));
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("reset recent directories", () => config.SetValue(OsuSetting.SlopFileSelectorRecentDirectories, string.Empty));
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
        public void TestImagePreview()
        {
            AddUntilStep("thumbnail loaded", () => selector.ChildrenOfType<FileThumbnail>().SingleOrDefault()?.Texture, () => Is.Not.Null);
            AddAssert("thumbnail keeps aspect ratio", () => selector.ChildrenOfType<FileThumbnail>().Single().Texture.Width, () => Is.EqualTo(2 * selector.ChildrenOfType<FileThumbnail>().Single().Texture.Height));
        }

        [Test]
        public void TestRecentDirectory()
        {
            AddStep("click recent directory", () =>
            {
                InputManager.MoveMouseTo(sidePanelItem("recent"));
                InputManager.Click(MouseButton.Left);
            });

            AddAssert("at recent directory", () => selector.CurrentPath.Value?.FullName, () => Is.EqualTo(new DirectoryInfo(recentDirectory).FullName));
        }

        [Test]
        public void TestThisPC()
        {
            AddStep("click this PC", () =>
            {
                InputManager.MoveMouseTo(sidePanelItem("This PC"));
                InputManager.Click(MouseButton.Left);
            });

            AddAssert("at device", () => selector.CurrentPath.Value, () => Is.Null);
        }

        [Test]
        public void TestSelectingFileRemembersDirectory()
        {
            AddStep("select file", () =>
            {
                InputManager.MoveMouseTo(selector.ChildrenOfType<OsuSpriteText>().Single(t => t.Text.ToString() == "text.txt"));
                InputManager.Click(MouseButton.Left);
            });

            AddAssert("directory is most recent", () => RecentDirectories.Parse(config.Get<string>(OsuSetting.SlopFileSelectorRecentDirectories)).First().FullName,
                () => Is.EqualTo(Path.TrimEndingDirectorySeparator(new DirectoryInfo(tempDirectory).FullName)));
        }

        [Test]
        public void TestRecentDirectoriesSetting()
        {
            AddAssert("most recent first without duplicates", () =>
            {
                string setting = RecentDirectories.Add(RecentDirectories.Add(RecentDirectories.Add(string.Empty, new DirectoryInfo(tempDirectory)), new DirectoryInfo(recentDirectory)),
                    new DirectoryInfo(tempDirectory));

                return RecentDirectories.Parse(setting).Select(d => d.FullName).ToArray();
            }, () => Is.EqualTo(new[] { new DirectoryInfo(tempDirectory).FullName, new DirectoryInfo(recentDirectory).FullName }));

            AddAssert("limited", () =>
            {
                string setting = string.Empty;

                for (int i = 0; i < RecentDirectories.MAX_COUNT + 3; i++)
                    setting = RecentDirectories.Add(setting, new DirectoryInfo(Path.Combine(tempDirectory, i.ToString())));

                return RecentDirectories.Parse(setting).Count();
            }, () => Is.EqualTo(RecentDirectories.MAX_COUNT));

            AddAssert("missing directories ignored", () => RecentDirectories.Get(Path.Combine(tempDirectory, "missing") + RecentDirectories.SEPARATOR + recentDirectory).Count, () => Is.EqualTo(1));
        }

        private Drawable sidePanelItem(string text) => selector.ChildrenOfType<FileSelectorSidePanel>().Single()
                                                               .ChildrenOfType<TruncatingSpriteText>().Single(t => t.Text.ToString() == text)
                                                               .FindClosestParent<OsuClickableContainer>()!;
    }
}
