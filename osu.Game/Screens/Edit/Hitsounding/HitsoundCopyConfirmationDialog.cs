// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Screens.Edit.Hitsounding
{
    public partial class HitsoundCopyConfirmationDialog : DangerousActionDialog
    {
        public HitsoundCopyConfirmationDialog(int difficultyCount, Action copy)
        {
            HeaderText = SlopHitsoundEditorStrings.CopyConfirmationHeader;
            BodyText = SlopHitsoundEditorStrings.CopyConfirmationBody(difficultyCount);

            DangerousAction = copy;
        }
    }
}
