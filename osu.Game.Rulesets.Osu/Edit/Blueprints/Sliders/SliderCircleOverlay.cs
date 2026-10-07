// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Game.Configuration;
using osu.Game.Rulesets.Osu.Edit.Blueprints.HitCircles.Components;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Edit.Blueprints.Sliders
{
    public partial class SliderCircleOverlay : CompositeDrawable
    {
        public SliderEndDragMarker? EndDragMarker { get; }

        public RectangleF VisibleQuad
        {
            get
            {
                var result = CirclePiece.ScreenSpaceDrawQuad.AABBFloat;

                // only account for the end drag marker if it's visible, so that the selection box fits the slider otherwise.
                if (EndDragMarker == null || !showEndDragMarker.Value) return result;

                // use the actual area of the arc, so that the gap between the selection box and the arc matches the gap around other objects.
                return RectangleF.Union(result, EndDragMarker.ScreenSpaceArcBounds);
            }
        }

        protected readonly HitCirclePiece CirclePiece;

        private readonly Slider slider;
        private readonly SliderPosition position;
        private readonly HitCircleOverlapMarker? marker;
        private readonly Container? endDragMarkerContainer;

        public SliderCircleOverlay(Slider slider, SliderPosition position)
        {
            this.slider = slider;
            this.position = position;

            if (position == SliderPosition.Start)
                AddInternal(marker = new HitCircleOverlapMarker());

            AddInternal(CirclePiece = new HitCirclePiece());

            if (position == SliderPosition.End)
            {
                // the outer container handles the visibility setting, while the inner one is shown / hidden alongside the circle piece.
                AddInternal(endDragMarkerVisibilityContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = endDragMarkerContainer = new Container
                    {
                        AutoSizeAxes = Axes.Both,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Padding = new MarginPadding(SliderEndDragMarker.ARC_CENTRE_OFFSET - SliderEndDragMarker.PATH_RADIUS),
                        Child = EndDragMarker = new SliderEndDragMarker()
                    }
                });
            }
        }

        private readonly Container? endDragMarkerVisibilityContainer;

        private readonly Bindable<bool> showEndDragMarker = new Bindable<bool>();

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopEditorShowSliderEndDragMarker, showEndDragMarker);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // when hidden, the marker is not present and therefore also doesn't receive input.
            showEndDragMarker.BindValueChanged(show =>
            {
                if (endDragMarkerVisibilityContainer != null)
                    endDragMarkerVisibilityContainer.Alpha = show.NewValue ? 1 : 0;
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            var circle = position == SliderPosition.Start ? (HitCircle)slider.HeadCircle :
                slider.RepeatCount % 2 == 0 ? slider.TailCircle : slider.LastRepeat!;

            CirclePiece.UpdateFrom(circle);
            marker?.UpdateFrom(circle);

            if (endDragMarkerContainer != null)
            {
                endDragMarkerContainer.Position = circle.Position + slider.StackOffset;
                endDragMarkerContainer.Scale = CirclePiece.Scale * 1.2f;
                var diff = slider.Path.PositionAt(1) - slider.Path.PositionAt(0.99f);
                endDragMarkerContainer.Rotation = float.RadiansToDegrees(MathF.Atan2(diff.Y, diff.X));
            }
        }

        public override void Hide()
        {
            CirclePiece.Hide();
            endDragMarkerContainer?.Hide();
        }

        public override void Show()
        {
            CirclePiece.Show();
            endDragMarkerContainer?.Show();
        }
    }
}
