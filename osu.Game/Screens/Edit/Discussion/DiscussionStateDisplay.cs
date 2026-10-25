// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Screens.Edit.Nominators;
using osuTK;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// What is shown instead of the discussion while it's loading or couldn't be loaded.
    /// </summary>
    public partial class DiscussionStateDisplay : CompositeDrawable
    {
        [Resolved]
        private DiscussionSession session { get; set; } = null!;

        private readonly IBindable<DiscussionSessionState> state = new Bindable<DiscussionSessionState>();

        private FillFlowContainer content = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = new NominatorsPanel(Axes.Y)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 520,
                Child = content = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 12),
                    Padding = new MarginPadding(30),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            state.BindTo(session.State);
            state.BindValueChanged(_ => updateDisplay(), true);
        }

        private void updateDisplay()
        {
            content.Clear();

            switch (state.Value)
            {
                case DiscussionSessionState.Loading:
                    add(FontAwesome.Solid.Comments, SlopDiscussionStrings.Loading);
                    content.Add(new LoadingSpinner
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Size = new Vector2(30),
                        State = { Value = Visibility.Visible },
                    });
                    break;

                case DiscussionSessionState.Failed:
                    add(FontAwesome.Solid.ExclamationTriangle, SlopDiscussionStrings.LoadingFailed);

                    if (!string.IsNullOrEmpty(session.Message))
                    {
                        content.Add(new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 15))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            TextAnchor = Anchor.TopCentre,
                            Text = session.Message,
                            Alpha = 0.7f,
                        });
                    }

                    content.Add(new RoundedButton
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Width = 260,
                        Height = 36,
                        Text = SlopDiscussionStrings.Retry,
                        Action = () => session.Reload(),
                    });
                    break;
            }
        }

        private void add(IconUsage icon, LocalisableString title)
        {
            content.Add(new SpriteIcon
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Icon = icon,
                Size = new Vector2(36),
            });

            content.Add(new OsuSpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Text = title,
                Font = OsuFont.Default.With(size: 22, weight: FontWeight.Bold),
            });
        }
    }
}
