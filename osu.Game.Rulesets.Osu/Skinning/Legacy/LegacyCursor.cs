// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shaders;
using osu.Game.Rulesets.Osu.UI.Cursor;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Osu.Skinning.Legacy
{
    public partial class LegacyCursor : SkinnableCursor
    {
        public static readonly int REVOLUTION_DURATION = 10000;

        private const float pressed_scale = 1.3f;
        private const float released_scale = 1f;

        private readonly ISkin skin;
        private bool spin;

        private CursorSprite cursorSprite = null!;
        private CursorSprite cursorMiddleSprite = null!;

        public LegacyCursor(ISkin skin)
        {
            this.skin = skin;
            Size = new Vector2(50);

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            bool centre = skin.GetConfig<OsuSkinConfiguration, bool>(OsuSkinConfiguration.CursorCentre)?.Value ?? true;
            spin = skin.GetConfig<OsuSkinConfiguration, bool>(OsuSkinConfiguration.CursorRotate)?.Value ?? true;

            InternalChildren = new[]
            {
                ExpandTarget = cursorSprite = new CursorSprite
                {
                    Texture = skin.GetTexture("cursor"),
                    Anchor = Anchor.Centre,
                    Origin = centre ? Anchor.Centre : Anchor.TopLeft,
                },
                cursorMiddleSprite = new CursorSprite
                {
                    Texture = skin.GetTexture("cursormiddle"),
                    Anchor = Anchor.Centre,
                    Origin = centre ? Anchor.Centre : Anchor.TopLeft,
                },
            };
        }

        protected override void LoadComplete()
        {
            if (spin)
                ExpandTarget.Spin(REVOLUTION_DURATION, RotationDirection.Clockwise);
        }

        public override void ApplyColourEffect(Colour4? colour)
        {
            base.ApplyColourEffect(colour);

            // skin textures may have their own colour, which would otherwise mix with the effect colour.
            cursorSprite.Monochrome = colour != null;
            cursorMiddleSprite.Monochrome = colour != null;
        }

        public override void Expand()
        {
            ExpandTarget?.ScaleTo(released_scale)
                        .ScaleTo(pressed_scale, 100, Easing.Out);
        }

        public override void Contract()
        {
            ExpandTarget?.ScaleTo(released_scale, 100, Easing.Out);
        }

        private partial class CursorSprite : NonPlayfieldSprite
        {
            private IShader defaultShader = null!;
            private IShader monochromeShader = null!;

            private bool monochrome;

            /// <summary>
            /// Whether to discard the colour of the texture, so that only <see cref="Drawable.Colour"/> is displayed.
            /// </summary>
            public bool Monochrome
            {
                get => monochrome;
                set
                {
                    if (monochrome == value)
                        return;

                    monochrome = value;

                    if (IsLoaded)
                        updateShader();
                }
            }

            [BackgroundDependencyLoader]
            private void load(ShaderManager shaders)
            {
                defaultShader = TextureShader;
                monochromeShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, @"TextureMonochrome");
                updateShader();
            }

            private void updateShader()
            {
                TextureShader = monochrome ? monochromeShader : defaultShader;
                Invalidate(Invalidation.DrawNode);
            }
        }
    }
}
