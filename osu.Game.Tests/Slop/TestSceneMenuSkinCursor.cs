// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Graphics.Cursor;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneMenuSkinCursor : OsuTestScene
    {
        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private MenuSkinCursor cursor = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("use argon skin", () => skins.CurrentSkinInfo.Value = ArgonSkin.CreateInfo().ToLiveUnmanaged());
            AddStep("create cursor", () => Child = cursor = new MenuSkinCursor
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            });
        }

        [Test]
        public void TestLegacySkinProvidesCursor()
        {
            AddAssert("not available with argon", () => !cursor.Available.Value);

            AddStep("use classic skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddUntilStep("available", () => cursor.Available.Value);

            AddStep("expand", () => cursor.Expand());
            AddStep("contract", () => cursor.Contract());

            AddStep("use argon skin", () => skins.CurrentSkinInfo.Value = ArgonSkin.CreateInfo().ToLiveUnmanaged());
            AddUntilStep("not available", () => !cursor.Available.Value);
        }
    }
}
