// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// The button which loads the discussion again, which is highlighted once the discussion has updates, like the refresh button of the website.
    /// </summary>
    public partial class DiscussionRefreshButton : OsuClickableContainer, IHasTooltip
    {
        private const float size = 50;

        [Resolved]
        private DiscussionSession session { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly IBindable<DiscussionRefreshState> refreshState = new Bindable<DiscussionRefreshState>();

        private Box background = null!;
        private SpriteIcon icon = null!;

        public override LocalisableString TooltipText
        {
            get
            {
                switch (refreshState.Value)
                {
                    case DiscussionRefreshState.Checking:
                        return SlopDiscussionStrings.RefreshChecking;

                    case DiscussionRefreshState.HasUpdates:
                        return SlopDiscussionStrings.RefreshHasUpdates;

                    case DiscussionRefreshState.Updating:
                        return SlopDiscussionStrings.RefreshUpdating;

                    default:
                        return SlopDiscussionStrings.RefreshNoUpdates;
                }
            }
            set { }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Size = new Vector2(size);
            Masking = true;
            CornerRadius = size / 2;
            EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Colour = Color4.Black.Opacity(0.25f),
                Radius = 10,
            };

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                icon = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(18),
                    Icon = FontAwesome.Solid.Redo,
                },
            };

            Action = () => session.Refresh();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            refreshState.BindTo(session.RefreshState);
            refreshState.BindValueChanged(_ => updateDisplay(), true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateDisplay();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateDisplay();
            base.OnHoverLost(e);
        }

        private void updateDisplay()
        {
            bool hasUpdates = refreshState.Value == DiscussionRefreshState.HasUpdates;
            bool busy = refreshState.Value is DiscussionRefreshState.Checking or DiscussionRefreshState.Updating;

            background.FadeColour(hasUpdates ? DiscussionDisplay.BLUE_DARKER : IsHovered ? colourProvider.Background2 : colourProvider.Background4, 200, Easing.OutQuint);
            icon.FadeColour(hasUpdates || IsHovered ? Color4.White : colourProvider.Content2, 200, Easing.OutQuint);

            icon.ClearTransforms(targetMember: nameof(Rotation));

            if (busy)
                icon.RotateTo(0).Then().RotateTo(360, 1000).Loop();
            else
                icon.RotateTo(0, 200, Easing.OutQuint);
        }
    }
}
