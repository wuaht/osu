// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Screens.Edit.Components.RadioButtons;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// Mirrors the selection over a line with an arbitrary angle, with a live preview (like osu!stable's angled flip).
    /// </summary>
    public partial class PreciseAngledFlipPopover : OsuPopover
    {
        private readonly OsuSelectionAngledFlipHandler flipHandler;

        private readonly OsuGridToolboxGroup gridToolbox;

        private readonly Bindable<PreciseAngledFlipInfo> flipInfo = new Bindable<PreciseAngledFlipInfo>(new PreciseAngledFlipInfo(0, EditorOrigin.PlayfieldCentre));

        private FormSliderBar<float> angleInput { get; set; } = null!;
        private EditorRadioButtonCollection flipOrigin = null!;

        private EditorRadioButton playfieldCentreButton = null!;

        public PreciseAngledFlipPopover(OsuSelectionAngledFlipHandler flipHandler, OsuGridToolboxGroup gridToolbox)
        {
            this.flipHandler = flipHandler;
            this.gridToolbox = gridToolbox;

            AllowableAnchors = new[] { Anchor.CentreLeft, Anchor.CentreRight };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new FillFlowContainer
            {
                Width = 220,
                AutoSizeAxes = Axes.Y,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    angleInput = new FormSliderBar<float>
                    {
                        Caption = SlopEditorStrings.AngledFlipLineAngle,
                        HintText = SlopEditorStrings.AngledFlipLineAngleDescription,
                        Current = new BindableNumber<float>
                        {
                            MinValue = -180,
                            MaxValue = 180,
                            Precision = 1
                        },
                        KeyboardStep = 1f,
                        TabbableContentContainer = this
                    },
                    flipOrigin = new EditorRadioButtonCollection
                    {
                        RelativeSizeAxes = Axes.X,
                    }
                }
            };

            flipOrigin.AddButton(new EditorRadioButton("Grid centre",
                () => flipInfo.Value = flipInfo.Value with { Origin = EditorOrigin.GridCentre },
                () => new SpriteIcon { Icon = FontAwesome.Regular.PlusSquare }));
            flipOrigin.AddButton(playfieldCentreButton = new EditorRadioButton("Playfield centre",
                () => flipInfo.Value = flipInfo.Value with { Origin = EditorOrigin.PlayfieldCentre },
                () => new SpriteIcon { Icon = FontAwesome.Regular.Square }));
            flipOrigin.AddButton(new EditorRadioButton("Selection centre",
                () => flipInfo.Value = flipInfo.Value with { Origin = EditorOrigin.SelectionCentre },
                () => new SpriteIcon { Icon = FontAwesome.Solid.VectorSquare }));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ScheduleAfterChildren(() => angleInput.TakeFocus());
            angleInput.Current.BindValueChanged(angle => flipInfo.Value = flipInfo.Value with { Degrees = angle.NewValue });

            playfieldCentreButton.Select();

            flipInfo.BindValueChanged(flip =>
            {
                // can happen if the popover is dismissed by a keyboard key press while dragging UI controls
                if (!flipHandler.OperationInProgress.Value)
                    return;

                flipHandler.Update(flip.NewValue.Degrees, getOriginPosition(flip.NewValue));
            }, true);
        }

        private Vector2? getOriginPosition(PreciseAngledFlipInfo flip) =>
            flip.Origin switch
            {
                EditorOrigin.GridCentre => gridToolbox.StartPosition.Value,
                EditorOrigin.PlayfieldCentre => OsuPlayfield.BASE_SIZE / 2,
                EditorOrigin.SelectionCentre => null,
                _ => throw new ArgumentOutOfRangeException(nameof(flip))
            };

        protected override void PopIn()
        {
            base.PopIn();
            flipHandler.Begin();
        }

        protected override void PopOut()
        {
            base.PopOut();

            if (IsLoaded && flipHandler.OperationInProgress.Value)
                flipHandler.Commit();
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == GlobalAction.Select && !e.Repeat)
            {
                this.HidePopover();
                return true;
            }

            return base.OnPressed(e);
        }
    }

    public record PreciseAngledFlipInfo(float Degrees, EditorOrigin Origin);
}
