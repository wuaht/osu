// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// Outlines visible objects which go offscreen (see <see cref="OffscreenObjects"/>) with a red rectangle.
    /// </summary>
    public partial class OffscreenObjectOverlay : CompositeDrawable
    {
        private const float outline_thickness = 2f;

        private readonly Playfield playfield;

        private Bindable<bool> enabled = null!;

        private readonly List<Container> outlines = new List<Container>();

        public OffscreenObjectOverlay(Playfield playfield)
        {
            this.playfield = playfield;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            enabled = config.GetBindable<bool>(OsuSetting.SlopEditorShowOffscreenObjects);
        }

        protected override void Update()
        {
            base.Update();

            int count = 0;

            if (enabled.Value)
            {
                foreach (var drawable in playfield.HitObjectContainer.AliveObjects)
                {
                    if (drawable.HitObject is not OsuHitObject hitObject || !OffscreenObjects.IsOffscreen(hitObject, out var bounds))
                        continue;

                    // positioned every frame as the playfield may move or be resized.
                    Vector2 topLeft = ToLocalSpace(playfield.GamefieldToScreenSpace(bounds.TopLeft));
                    Vector2 bottomRight = ToLocalSpace(playfield.GamefieldToScreenSpace(bounds.BottomRight));

                    var outline = getOutline(count++);
                    outline.Position = Vector2.ComponentMin(topLeft, bottomRight);
                    outline.Size = Vector2.ComponentMax(topLeft, bottomRight) - outline.Position;

                    // fades in and out along with the object.
                    outline.Alpha = drawable.Alpha;
                }
            }

            for (int i = count; i < outlines.Count; i++)
                outlines[i].Alpha = 0;
        }

        private Container getOutline(int index)
        {
            if (index < outlines.Count)
                return outlines[index];

            var outline = new Container
            {
                Masking = true,
                BorderThickness = outline_thickness,
                BorderColour = Color4.Red,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    AlwaysPresent = true,
                },
            };

            outlines.Add(outline);
            AddInternal(outline);

            return outline;
        }
    }
}
