// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Localisation.Osu;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Tools;

namespace osu.Game.Rulesets.Osu.Edit.Streams
{
    public class StreamCompositionTool : CompositionTool<OsuAction>
    {
        public StreamCompositionTool()
            : base(OsuEditorStrings.StreamTool)
        {
            Action = OsuAction.EditorStreamTool;
        }

        public override Drawable CreateIcon() => new SpriteIcon { Icon = FontAwesome.Solid.EllipsisH };

        public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new StreamPlacementBlueprint();
    }
}
