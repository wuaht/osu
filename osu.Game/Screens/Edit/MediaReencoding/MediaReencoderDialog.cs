// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osuTK;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Screens.Edit.MediaReencoding
{
    /// <summary>
    /// Options for <see cref="MediaReencoder"/>, ported from the stable client's media reencoder form.
    /// </summary>
    public partial class MediaReencoderDialog : PopupDialog
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);

        private readonly Bindable<MediaReencoderMediaType> mediaType = new Bindable<MediaReencoderMediaType>();
        private readonly Bindable<MediaReencoderVideoCodec> videoCodec = new Bindable<MediaReencoderVideoCodec>(MediaReencoderVideoCodec.H264);
        private readonly Bindable<MediaReencoderResize> resize = new Bindable<MediaReencoderResize>(MediaReencoderResize.Height720);
        private readonly Bindable<MediaReencoderAudioCodec> audioCodec = new Bindable<MediaReencoderAudioCodec>();

        private readonly BindableInt quality = new BindableInt(23)
        {
            MinValue = 0,
            MaxValue = MediaReencoder.MAX_QUALITY,
        };

        private readonly FillFlowContainer videoOptions;
        private readonly Drawable audioOptions;

        public MediaReencoderDialog(bool hasVideo, Action<MediaReencoderOptions> convert)
        {
            HeaderText = SlopEditorStrings.MediaReencoder;
            BodyText = SlopEditorStrings.MediaReencoderBody;
            Icon = FontAwesome.Solid.Film;

            if (!hasVideo)
            {
                mediaType.Value = MediaReencoderMediaType.Audio;
                mediaType.Disabled = true;
            }

            MainContent.Child = new FillFlowContainer
            {
                Margin = new MarginPadding { Top = 20 },
                Padding = new MarginPadding { Horizontal = 20 },
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(7),
                Children = new Drawable[]
                {
                    new FormEnumDropdown<MediaReencoderMediaType>
                    {
                        Caption = SlopEditorStrings.MediaReencoderMediaType,
                        Current = mediaType,
                    },
                    videoOptions = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(7),
                        Children = new Drawable[]
                        {
                            new FormEnumDropdown<MediaReencoderVideoCodec>
                            {
                                Caption = SlopEditorStrings.MediaReencoderEncoder,
                                Current = videoCodec,
                            },
                            new FormEnumDropdown<MediaReencoderResize>
                            {
                                Caption = SlopEditorStrings.MediaReencoderResize,
                                Current = resize,
                            },
                            new FormSliderBar<int>
                            {
                                Caption = SlopEditorStrings.MediaReencoderQuality,
                                HintText = SlopEditorStrings.MediaReencoderQualityHint,
                                Current = quality,
                            },
                        }
                    },
                    audioOptions = new FormEnumDropdown<MediaReencoderAudioCodec>
                    {
                        Caption = SlopEditorStrings.MediaReencoderEncoder,
                        Current = audioCodec,
                    },
                }
            };

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = SlopEditorStrings.MediaReencoderConvert,
                    Action = () => convert(new MediaReencoderOptions(mediaType.Value, videoCodec.Value, resize.Value, audioCodec.Value, quality.Value)),
                },
                new PopupDialogCancelButton
                {
                    Text = WebCommonStrings.ButtonsCancel,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            mediaType.BindValueChanged(e =>
            {
                bool video = e.NewValue == MediaReencoderMediaType.Video;
                videoOptions.FadeTo(video ? 1 : 0);
                audioOptions.FadeTo(video ? 0 : 1);
            }, true);
        }
    }
}
