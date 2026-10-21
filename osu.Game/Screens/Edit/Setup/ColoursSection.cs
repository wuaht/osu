// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Skinning;
using osu.Game.Utils;

namespace osu.Game.Screens.Edit.Setup
{
    public partial class ColoursSection : SetupSection
    {
        public override LocalisableString Title => EditorSetupStrings.ColoursHeader;

        private const string slider_track_key = @"SliderTrackOverride";
        private const string slider_border_key = @"SliderBorder";

        private FormColourPalette comboColours = null!;

        private FormCheckBox customSliderColours = null!;
        private FormColourPicker sliderTrackColour = null!;
        private FormColourPicker sliderBorderColour = null!;

        /// <summary>
        /// The colours used when no colours could be extracted from the background (like osu!stable).
        /// </summary>
        private static readonly Colour4[] fallback_random_colours =
        {
            Colour4.Red, Colour4.Green, Colour4.Blue, Colour4.Yellow, Colour4.Orange, Colour4.Purple, Colour4.Cyan, Colour4.Magenta,
        };

        private FormButton randomColoursButton = null!;

        private readonly Random random = new Random();

        private IReadOnlyList<Colour4> randomColourCandidates = fallback_random_colours;

        private CancellationTokenSource? cancellationSource;

        [Resolved]
        private IBindable<WorkingBeatmap> working { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(SetupScreen? setupScreen)
        {
            Children = new Drawable[]
            {
                comboColours = new FormColourPalette
                {
                    PaletteHeaderText = EditorSetupStrings.AccentColoursInBeatmapBackground,
                    Caption = EditorSetupStrings.HitCircleSliderCombos,
                    HintText = SlopEditorStrings.RandomComboColoursDescription,
                    AllowLocking = true,
                },
                randomColoursButton = new FormButton
                {
                    Caption = SlopEditorStrings.RandomComboColours,
                    ButtonText = SlopEditorStrings.GetRandomColours,
                    Action = pickRandomColours,
                },
                customSliderColours = new FormCheckBox
                {
                    Caption = SlopEditorStrings.CustomSliderColours,
                    HintText = SlopEditorStrings.CustomSliderColoursDescription,
                },
                sliderTrackColour = new FormColourPicker
                {
                    Caption = SlopEditorStrings.SliderTrackColour,
                    Current = new Bindable<string>(Colour4.Black.ToHex()),
                },
                sliderBorderColour = new FormColourPicker
                {
                    Caption = SlopEditorStrings.SliderBorderColour,
                    Current = new Bindable<string>(Colour4.White.ToHex()),
                },
            };

            setupScreen?.BackgroundChanged += refreshSuggestions;

            refreshSuggestions();
        }

        private bool syncingColours;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (Beatmap.BeatmapSkin != null)
                comboColours.Colours.AddRange(Beatmap.BeatmapSkin.ComboColours);

            if (comboColours.Colours.Count == 0)
            {
                // compare ctor of `EditorBeatmapSkin`
                for (int i = 0; i < SkinConfiguration.DefaultComboColours.Count; ++i)
                    comboColours.Colours.Add(SkinConfiguration.DefaultComboColours[(i + 1) % SkinConfiguration.DefaultComboColours.Count]);
            }

            comboColours.Colours.BindCollectionChanged((_, _) =>
            {
                if (Beatmap.BeatmapSkin != null)
                {
                    if (syncingColours)
                        return;

                    syncingColours = true;

                    Beatmap.BeatmapSkin.ComboColours.Clear();
                    Beatmap.BeatmapSkin.ComboColours.AddRange(comboColours.Colours);

                    updateAddButtonVisibility();

                    syncingColours = false;
                }
            });

            Beatmap.BeatmapSkin?.ComboColours.BindCollectionChanged((_, _) =>
            {
                if (syncingColours)
                    return;

                syncingColours = true;

                comboColours.Colours.Clear();
                comboColours.Colours.AddRange(Beatmap.BeatmapSkin?.ComboColours);

                updateAddButtonVisibility();

                syncingColours = false;
            });

            updateAddButtonVisibility();

            loadSliderColours();

            void updateAddButtonVisibility() => comboColours.CanAdd.Value = comboColours.Colours.Count < LegacyBeatmapDecoder.MAX_COMBO_COLOUR_COUNT;
        }

        /// <summary>
        /// Allows overriding the slider track and border colours of the skin for this beatmap (like osu!stable).
        /// </summary>
        private void loadSliderColours()
        {
            var customColours = (Beatmap.BeatmapSkin as IHasCustomColours)?.CustomColours;

            if (customColours == null)
            {
                customSliderColours.Current.Disabled = true;
                sliderTrackColour.Current.Disabled = true;
                sliderBorderColour.Current.Disabled = true;
                return;
            }

            bool hasTrack = customColours.TryGetValue(slider_track_key, out var track);
            bool hasBorder = customColours.TryGetValue(slider_border_key, out var border);

            if (hasTrack)
                sliderTrackColour.Current.Value = ((Colour4)track).ToHex();
            if (hasBorder)
                sliderBorderColour.Current.Value = ((Colour4)border).ToHex();

            customSliderColours.Current.Value = hasTrack || hasBorder;

            bool initialised = false;

            customSliderColours.Current.BindValueChanged(_ => updateSliderColours(), true);
            sliderTrackColour.Current.BindValueChanged(_ => updateSliderColours());
            sliderBorderColour.Current.BindValueChanged(_ => updateSliderColours());

            void updateSliderColours()
            {
                bool enabled = customSliderColours.Current.Value;

                sliderTrackColour.Current.Disabled = !enabled;
                sliderBorderColour.Current.Disabled = !enabled;

                // the initial state is only displayed, so that opening the setup screen doesn't modify the beatmap.
                if (!initialised)
                {
                    initialised = true;
                    return;
                }

                if (enabled)
                {
                    // like osu!stable, both colours are written when the custom slider colours are enabled.
                    customColours[slider_track_key] = SlopColours.Parse(sliderTrackColour.Current.Value, Colour4.Black);
                    customColours[slider_border_key] = SlopColours.Parse(sliderBorderColour.Current.Value, Colour4.White);
                }
                else
                {
                    customColours.Remove(slider_track_key);
                    customColours.Remove(slider_border_key);
                }

                Beatmap.BeatmapSkin!.InvokeSkinChanged();
                Beatmap.SaveState();
            }
        }

        private void pickRandomColours()
        {
            var newColours = BackgroundComboColourExtractor.PickRandomColours(comboColours.Colours, comboColours.IsLocked, randomColourCandidates, random);

            // the colours are replaced in-place to keep the locks, while the beatmap skin is only updated once to create a single undo step.
            syncingColours = true;

            for (int i = 0; i < newColours.Length; i++)
            {
                if (comboColours.Colours[i] != newColours[i])
                    comboColours.Colours[i] = newColours[i];
            }

            syncingColours = false;

            if (Beatmap.BeatmapSkin != null && !Beatmap.BeatmapSkin.ComboColours.SequenceEqual(comboColours.Colours))
            {
                syncingColours = true;
                Beatmap.BeatmapSkin.ComboColours.Clear();
                Beatmap.BeatmapSkin.ComboColours.AddRange(comboColours.Colours);
                syncingColours = false;
            }
        }

        private void refreshSuggestions()
        {
            cancellationSource?.Cancel();
            cancellationSource = new CancellationTokenSource();

            var cancellationToken = cancellationSource.Token;

            string backgroundFile = working.Value.Metadata.BackgroundFile;

            randomColourCandidates = fallback_random_colours;

            if (string.IsNullOrEmpty(backgroundFile))
            {
                comboColours.Suggestions.Clear();
                return;
            }

            string? storagePath = working.Value.BeatmapSetInfo.GetPathForFile(backgroundFile);

            if (storagePath == null)
            {
                comboColours.Suggestions.Clear();
                return;
            }

            var beatmap = working.Value;

            Task.Run(() =>
            {
                try
                {
                    using var stream = beatmap.GetStream(storagePath);

                    if (stream == null)
                        return;

                    var colours = BackgroundComboColourExtractor.Extract(stream);

                    stream.Seek(0, SeekOrigin.Begin);
                    var candidates = BackgroundComboColourExtractor.Extract(stream, BackgroundComboColourExtractor.RANDOM_CANDIDATE_COUNT);

                    Schedule(() =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        comboColours.Suggestions.Clear();
                        comboColours.Suggestions.AddRange(colours);

                        if (candidates.Length > 0)
                            randomColourCandidates = candidates;
                    });
                }
                catch (Exception e)
                {
                    Logger.Error(e, @"Failed to extract combo colours from background");
                }
            }, cancellationToken);
        }
    }
}
