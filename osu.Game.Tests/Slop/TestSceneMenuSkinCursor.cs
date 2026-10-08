// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Graphics.Cursor;
using osu.Game.Rulesets.Osu.UI.Cursor;
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
            AddStep("create cursor", () => Child = cursor = new MenuSkinCursor());
            AddUntilStep("cursor available", () => cursor.Available.Value);
            AddStep("show cursor", () => cursor.SetVisible(true));
        }

        [Test]
        public void TestGameplayCursorShownForAllSkins()
        {
            AddAssert("shows osu! gameplay cursor", () => cursor.ChildrenOfType<OsuCursorContainer>().Any());
            AddUntilStep("shows cursor trail", () => cursor.ChildrenOfType<CursorTrail>().Any());

            AddStep("use classic skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddUntilStep("shows legacy cursor", () => cursor.ChildrenOfType<SkinnableCursor>().Any(c => c.GetType().Name == "LegacyCursor"));
            AddUntilStep("shows legacy cursor trail", () => cursor.ChildrenOfType<CursorTrail>().Any(c => c.GetType().Name == "LegacyCursorTrail"));

            AddStep("use triangles skin", () => skins.CurrentSkinInfo.Value = TrianglesSkin.CreateInfo().ToLiveUnmanaged());
            AddUntilStep("no legacy cursor", () => !cursor.ChildrenOfType<SkinnableCursor>().Any(c => c.GetType().Name == "LegacyCursor"));
        }
    }
}
