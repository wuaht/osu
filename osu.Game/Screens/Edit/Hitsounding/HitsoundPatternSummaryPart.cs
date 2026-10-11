// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Screens.Edit.Components.Timelines.Summary.Parts;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Shows where hitsound patterns are used in the summary timeline at the bottom of the editor, in their colours, while the hitsound screen is open.
    /// </summary>
    public partial class HitsoundPatternSummaryPart : TimelinePart
    {
        [Resolved(canBeNull: true)]
        private Editor? editor { get; set; }

        private readonly Bindable<EditorScreenMode> mode = new Bindable<EditorScreenMode>();

        /// <summary>
        /// The patterns of the open hitsound screen, or <c>null</c> while it isn't open (or not loaded yet).
        /// </summary>
        private HitsoundPatterns? patterns;

        private bool regionsInvalidated;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (editor != null)
                mode.BindTo(editor.Mode);

            mode.BindValueChanged(_ => setPatterns(null));
        }

        protected override void Update()
        {
            base.Update();

            // the hitsound screen is loaded asynchronously after switching to it, and owns the patterns, so it is looked up until it's there.
            if (patterns == null && mode.Value == EditorScreenMode.Hitsound && editor != null)
            {
                var screen = editor.ChildrenOfType<HitsoundScreen>().FirstOrDefault(s => s.IsLoaded);

                if (screen != null)
                    setPatterns(screen.Patterns);
            }

            if (regionsInvalidated)
            {
                regionsInvalidated = false;
                updateRegions();
            }
        }

        private void setPatterns(HitsoundPatterns? newPatterns)
        {
            if (patterns == newPatterns)
                return;

            if (patterns != null)
                patterns.Changed -= invalidateRegions;

            patterns = newPatterns;

            if (patterns != null)
                patterns.Changed += invalidateRegions;

            invalidateRegions();
        }

        private void invalidateRegions() => regionsInvalidated = true;

        private void updateRegions()
        {
            Clear();

            if (patterns == null)
                return;

            foreach (var occurrence in patterns.Occurrences)
            {
                // sub-patterns are drawn smaller, inside the patterns containing them.
                float height = occurrence.Depth == 0 ? 1 : 0.5f;

                Add(new Box
                {
                    RelativePositionAxes = Axes.X,
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = (float)occurrence.StartTime,
                    Width = (float)occurrence.Pattern.Length,
                    Height = height,
                    Colour = occurrence.Pattern.Colour,
                    Alpha = occurrence.Depth == 0 ? 0.35f : 0.5f,
                });
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (patterns != null)
                patterns.Changed -= invalidateRegions;

            base.Dispose(isDisposing);
        }
    }
}
