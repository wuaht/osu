// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.Chat;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    /// <summary>
    /// Settings of the FPoSu ruleset (a first person mode based on McOsu's FPoSu).
    /// </summary>
    public partial class FposuSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.FposuHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "fps", "first person", "sensitivity", "fov", "mcosu" });

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SensitivityConverterNote(),
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = SlopSettingsStrings.FposuMouseDpi,
                    HintText = SlopSettingsStrings.FposuMouseDpiDescription,
                    Current = config.GetBindable<int>(OsuSetting.SlopFposuMouseDpi),
                    KeyboardStep = 50,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = SlopSettingsStrings.FposuCmPer360,
                    HintText = SlopSettingsStrings.FposuCmPer360Description,
                    Current = config.GetBindable<double>(OsuSetting.SlopFposuCmPer360),
                    KeyboardStep = 0.1f,
                    // McOsu allows six decimal places.
                    MaxDecimalDigits = 6,
                }),
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = SlopSettingsStrings.FposuFov,
                    HintText = SlopSettingsStrings.FposuFovDescription,
                    Current = config.GetBindable<float>(OsuSetting.SlopFposuFov),
                    KeyboardStep = 1,
                }),
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = SlopSettingsStrings.FposuDistance,
                    HintText = SlopSettingsStrings.FposuDistanceDescription,
                    Current = config.GetBindable<float>(OsuSetting.SlopFposuDistance),
                    KeyboardStep = 0.01f,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.FposuCurved,
                    HintText = SlopSettingsStrings.FposuCurvedDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopFposuCurved),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.FposuSkybox,
                    HintText = SlopSettingsStrings.FposuSkyboxDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopFposuSkybox),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.FposuBackgroundCube,
                    HintText = SlopSettingsStrings.FposuBackgroundCubeDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopFposuBackgroundCube),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.FposuTransparentBackground,
                    HintText = SlopSettingsStrings.FposuTransparentBackgroundDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopFposuTransparentBackground),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.FposuInvertHorizontal,
                    Current = config.GetBindable<bool>(OsuSetting.SlopFposuInvertHorizontal),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.FposuInvertVertical,
                    Current = config.GetBindable<bool>(OsuSetting.SlopFposuInvertVertical),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.FposuAbsoluteMode,
                    HintText = SlopSettingsStrings.FposuAbsoluteModeDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopFposuAbsoluteMode),
                }),
            };
        }

        /// <summary>
        /// An informational note with a clickable link to a sensitivity converter, like McOsu has.
        /// </summary>
        private partial class SensitivityConverterNote : CompositeDrawable
        {
            private const string url = @"https://www.mouse-sensitivity.com/";

            private IBindable<string> noteText = new Bindable<string>();
            private LinkFlowContainer linkContainer = null!;

            [Resolved]
            private LocalisationManager localisation { get; set; } = null!;

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Padding = SettingsPanel.CONTENT_PADDING;

                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    CornerRadius = 5,
                    CornerExponent = 2.5f,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Dark2,
                        },
                        linkContainer = new LinkFlowContainer(s =>
                        {
                            s.Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold);
                            s.Colour = colourProvider.Content2;
                        })
                        {
                            Padding = new MarginPadding(8),
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        },
                    }
                };

                noteText = localisation.GetLocalisedBindableString(SlopSettingsStrings.FposuSensitivityConverterNote(url));
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                noteText.BindValueChanged(_ =>
                {
                    linkContainer.Clear();

                    var formatted = MessageFormatter.FormatText(noteText.Value);
                    linkContainer.AddLinks(formatted.Text, formatted.Links);
                }, true);
            }
        }
    }
}
