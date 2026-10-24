// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// Shows an image of a message over the whole screen, where it can be zoomed and moved around like in Discord.
    /// </summary>
    public partial class DiscordMediaViewer : OsuFocusedOverlayContainer
    {
        private const float max_zoom = 8;
        private const float zoom_duration = 200;

        /// <summary>
        /// The space around the image, which leaves room for the buttons.
        /// </summary>
        private const float screen_margin = 60;

        protected override string? PopInSampleName => null;
        protected override string? PopOutSampleName => null;

        // Moving the image around shouldn't close the viewer when the mouse is released.
        public override bool DragBlocksClick => true;

        [Resolved]
        private GameHost host { get; set; } = null!;

        private Container imageContainer = null!;
        private Container imageContent = null!;
        private LoadingSpinner spinner = null!;

        private string? openUrl;
        private Vector2? naturalSize;

        private float zoom = 1;
        private Vector2 offset;

        private DiscordTextureStore? textures;

        public DiscordMediaViewer()
        {
            RelativeSizeAxes = Axes.Both;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.Cache(textures = new DiscordTextureStore(parent.Get<GameHost>()));
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.85f,
                },
                spinner = new LoadingSpinner
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
                imageContainer = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Child = imageContent = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Margin = new MarginPadding(16),
                    Children = new Drawable[]
                    {
                        new ViewerButton(FontAwesome.Solid.ExternalLinkAlt, SlopDiscordStrings.OpenInBrowser, () =>
                        {
                            if (openUrl != null)
                                host.OpenUrlExternally(openUrl);
                        }),
                        new ViewerButton(FontAwesome.Solid.Times, SlopDiscordStrings.CloseViewer, Hide),
                    },
                },
            };
        }

        /// <summary>
        /// Shows an image.
        /// </summary>
        /// <param name="url">The URL which is opened in the browser from the viewer.</param>
        /// <param name="displayUrl">The URL the image is loaded from.</param>
        /// <param name="width">The width of the image, if known.</param>
        /// <param name="height">The height of the image, if known.</param>
        /// <param name="animated">Whether the image is an animated GIF.</param>
        public void ShowImage(string url, string displayUrl, int? width, int? height, bool animated)
        {
            openUrl = url;
            naturalSize = width > 0 && height > 0 ? new Vector2(width.Value, height.Value) : null;

            imageContent.Clear();

            var image = new DiscordRemoteImage(displayUrl, animated)
            {
                RelativeSizeAxes = Axes.Both,
                ImageFillMode = FillMode.Fit,
                ShowPlaceholder = false,
            };

            image.Loaded += () => spinner.Hide();

            imageContent.Add(image);
            spinner.Show();

            resetZoom(false);
            Show();
        }

        protected override void Update()
        {
            base.Update();

            Vector2 bounds = Vector2.ComponentMax(Vector2.One, DrawSize - new Vector2(screen_margin * 2));

            if (naturalSize is Vector2 size)
            {
                // Like Discord, images are shown at their own size at most, and can be zoomed to see them larger.
                float scale = Math.Min(1, Math.Min(bounds.X / size.X, bounds.Y / size.Y));
                imageContainer.Size = size * scale;
            }
            else
                imageContainer.Size = bounds;
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            float factor = MathF.Pow(1.25f, e.ScrollDelta.Y);
            zoomTo(zoom * factor, e.MousePosition);
            return true;
        }

        protected override bool OnDoubleClick(DoubleClickEvent e)
        {
            if (!isOverImage(e.ScreenSpaceMousePosition))
                return false;

            if (zoom > 1)
                resetZoom(true);
            else
                zoomTo(2.5f, e.MousePosition);

            return true;
        }

        protected override bool OnClick(ClickEvent e)
        {
            // Clicking next to the image closes the viewer, like in Discord.
            if (!isOverImage(e.ScreenSpaceMousePosition))
                Hide();

            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => zoom > 1;

        protected override void OnDrag(DragEvent e)
        {
            offset = clampOffset(offset + e.Delta, zoom);

            imageContainer.ClearTransforms(targetMember: nameof(Position));
            imageContainer.Position = offset;
        }

        private bool isOverImage(Vector2 screenSpacePosition) => imageContainer.ReceivePositionalInputAt(screenSpacePosition);

        private void zoomTo(float newZoom, Vector2 mousePosition)
        {
            newZoom = Math.Clamp(newZoom, 1, max_zoom);

            if (newZoom == zoom)
                return;

            // Keep the point of the image below the cursor in place.
            Vector2 cursor = mousePosition - DrawSize / 2;
            Vector2 newOffset = cursor - (cursor - offset) * (newZoom / zoom);

            zoom = newZoom;
            offset = clampOffset(newOffset, zoom);

            imageContainer.ScaleTo(zoom, zoom_duration, Easing.OutQuint);
            imageContainer.MoveTo(offset, zoom_duration, Easing.OutQuint);
        }

        private void resetZoom(bool animate)
        {
            zoom = 1;
            offset = Vector2.Zero;

            imageContainer.ScaleTo(1, animate ? zoom_duration : 0, Easing.OutQuint);
            imageContainer.MoveTo(Vector2.Zero, animate ? zoom_duration : 0, Easing.OutQuint);
        }

        /// <summary>
        /// Keeps the zoomed image covering the screen, so it can't be moved out of view.
        /// </summary>
        private Vector2 clampOffset(Vector2 value, float scale)
        {
            Vector2 overflow = Vector2.ComponentMax(Vector2.Zero, (imageContainer.Size * scale - DrawSize) / 2);
            return Vector2.Clamp(value, -overflow, overflow);
        }

        protected override void PopIn()
        {
            this.FadeIn(zoom_duration, Easing.OutQuint);
            imageContainer.FadeInFromZero(zoom_duration, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            base.PopOut();

            this.FadeOut(zoom_duration, Easing.OutQuint).OnComplete(_ => imageContent.Clear());
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            textures?.Dispose();
        }

        private partial class ViewerButton : OsuClickableContainer
        {
            private readonly SpriteIcon icon;

            public ViewerButton(IconUsage iconUsage, LocalisableString tooltip, Action action)
            {
                Size = new Vector2(40);
                Masking = true;
                CornerRadius = 20;
                TooltipText = tooltip;
                Action = action;

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.5f,
                    },
                    icon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Icon = iconUsage,
                        Size = new Vector2(16),
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                icon.ScaleTo(1.15f, 200, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                icon.ScaleTo(1, 200, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }
    }
}
