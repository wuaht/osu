// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Footer;
using osu.Game.Users;
using osu.Game.Utils;
using osuTK;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// A song select which opens the selected beatmap in the editor instead of starting gameplay.
    /// </summary>
    public partial class EditorSongSelect : SongSelect, ICanAcceptFiles
    {
        private const float title_height = 50;

        protected override UserActivity InitialActivity => new UserActivity.ChoosingBeatmap();

        protected override OverlayColourScheme ColourScheme => OverlayColourScheme.Orange;

        [Resolved]
        private BeatmapSetOverlay? beatmapOverlay { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private Sample? sampleConfirmSelection;

        private Bindable<bool> showPlayButtons = null!;

        private Drawable title = null!;

        public EditorSongSelect()
        {
            TopPadding = title_height - 10;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, OsuConfigManager config)
        {
            sampleConfirmSelection = audio.Samples.Get(@"SongSelect/confirm-selection");

            showPlayButtons = config.GetBindable<bool>(OsuSetting.SlopEditorSongSelectShowPlayButtons);

            AddInternal(title = new EditorTitle
            {
                RelativeSizeAxes = Axes.X,
                Height = title_height,
                Padding = new MarginPadding { Left = 18 },
                Alpha = 0,
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // the footer buttons are only created when this screen becomes current, so they need to be refreshed explicitly.
            // the footer content compares equal to the current one, so it's reset first for the change to reach the footer.
            // the footer handles content changes once per frame, so the intermediate content is never displayed.
            showPlayButtons.BindValueChanged(_ =>
            {
                if (!this.IsCurrentScreen())
                    return;

                FooterContent.Value = ScreenFooterContent.None;
                FooterContent.Value = ScreenFooterContent.Argon(leftButtons: CreateFooterButtons);
            });
        }

        protected override BeatmapDetailsArea CreateDetailsArea() => new BeatmapDetailsArea { EditorMode = true };

        public override IReadOnlyList<ScreenFooterButton> CreateFooterButtons()
        {
            var buttons = new List<ScreenFooterButton> { new FooterButtonNewBeatmap { Action = createNewBeatmap } };

            if (showPlayButtons.Value)
                buttons.AddRange(base.CreateFooterButtons());

            return buttons;
        }

        protected override void OnStart()
        {
            if (!this.IsCurrentScreen())
                return;

            sampleConfirmSelection?.Play();
            this.Push(new EditorLoader());
        }

        private void createNewBeatmap() => createNewBeatmap(null);

        private void createNewBeatmap(FileInfo? audioTrack)
        {
            if (!this.IsCurrentScreen())
                return;

            sampleConfirmSelection?.Play();
            this.Push(new NewBeatmapEditorLoader(audioTrack));
        }

        IEnumerable<string> ICanAcceptFiles.HandledExtensions => SupportedExtensions.AUDIO_EXTENSIONS;

        Task ICanAcceptFiles.Import(params string[] paths)
        {
            string? audioPath = paths.FirstOrDefault(p => SupportedExtensions.AUDIO_EXTENSIONS.Contains(Path.GetExtension(p).ToLowerInvariant()));

            if (audioPath != null)
                Schedule(() => createNewBeatmap(new FileInfo(audioPath)));

            return Task.CompletedTask;
        }

        Task ICanAcceptFiles.Import(ImportTask[] tasks, ImportParameters parameters) => throw new NotImplementedException();

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (!showPlayButtons.Value)
            {
                switch (e.Action)
                {
                    // mods can't be changed without the mods button.
                    case GlobalAction.IncreaseModSpeed:
                    case GlobalAction.DecreaseModSpeed:
                        return false;
                }
            }

            return base.OnPressed(e);
        }

        public override IEnumerable<OsuMenuItem> GetForwardActions(BeatmapInfo beatmap)
        {
            yield return new OsuMenuItem(ButtonSystemStrings.Edit.ToSentence(), MenuItemType.Highlighted, () => SelectAndRun(beatmap, OnStart)) { Icon = FontAwesome.Solid.PencilAlt };

            yield return new OsuMenuItemSpacer();

            if (beatmap.OnlineID > 0)
            {
                yield return new OsuMenuItem(CommonStrings.Details, MenuItemType.Standard, () => beatmapOverlay?.FetchAndShowBeatmap(beatmap.OnlineID));

                if (beatmap.GetOnlineURL(api, Ruleset.Value) is string url)
                    yield return new OsuMenuItem(CommonStrings.CopyLink, MenuItemType.Standard, () => game?.CopyToClipboard(url));
            }

            var fileActions = CreateBeatmapFileMenuActions(beatmap).ToList();

            foreach (var i in fileActions)
                yield return i;

            // the online and file actions are only separated from the collections if there are any.
            if (beatmap.OnlineID > 0 || fileActions.Count > 0)
                yield return new OsuMenuItemSpacer();

            foreach (var i in CreateCollectionMenuActions(beatmap))
                yield return i;

            if (beatmap.BeatmapSet != null)
            {
                var beatmapSet = beatmap.BeatmapSet;

                yield return new OsuMenuItem(CommonStrings.Export)
                {
                    Icon = FontAwesome.Solid.FileExport,
                    Items = new[]
                    {
                        new OsuMenuItem(EditorStrings.ExportForEditing, MenuItemType.Standard, () => exportSet(beatmapSet, beatmaps.Export)),
                        new OsuMenuItem(EditorStrings.ExportForCompatibility, MenuItemType.Standard, () => exportSet(beatmapSet, beatmaps.ExportLegacy)),
                        new OsuMenuItem(EditorStrings.ExportGuestDifficulty, MenuItemType.Standard, () => exportBeatmap(beatmap)),
                    }
                };
            }

            if (beatmaps.CanHide(beatmap))
                yield return new OsuMenuItem(WebCommonStrings.ButtonsHide.ToSentence(), MenuItemType.Standard, () => beatmaps.Hide(beatmap));

            if (beatmap.BeatmapSet != null)
                yield return new OsuMenuItem(CommonStrings.DeleteWithConfirmation, MenuItemType.Destructive, () => Delete(beatmap.BeatmapSet)) { Icon = FontAwesome.Solid.Trash };
        }

        // beatmaps provided by the carousel are detached and don't carry their files, so the exporters need the managed instances.
        private void exportSet(BeatmapSetInfo beatmapSet, Func<BeatmapSetInfo, Task> export)
        {
            var managed = realm.Realm.Find<BeatmapSetInfo>(beatmapSet.ID);

            if (managed != null)
                export(managed);
        }

        private void exportBeatmap(BeatmapInfo beatmap)
        {
            var managed = realm.Realm.Find<BeatmapInfo>(beatmap.ID);

            if (managed != null)
                beatmaps.ExportLegacy(managed);
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            game?.RegisterImportHandler(this);
            title.FadeIn(ENTER_DURATION, Easing.OutQuint);
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);
            game?.RegisterImportHandler(this);
            title.FadeIn(ENTER_DURATION, Easing.OutQuint);
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            game?.UnregisterImportHandler(this);
            title.FadeOut(ENTER_DURATION / 2, Easing.OutQuint);
            base.OnSuspending(e);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (base.OnExiting(e))
                return true;

            game?.UnregisterImportHandler(this);
            title.FadeOut(ENTER_DURATION / 2, Easing.OutQuint);
            return false;
        }

        /// <summary>
        /// Shown at the top of the screen to make it clear that beatmaps are selected for editing rather than for gameplay.
        /// </summary>
        private partial class EditorTitle : Container
        {
            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                Child = new FillFlowContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Solid.PencilAlt,
                            Size = new Vector2(20),
                            Margin = new MarginPadding { Right = 4 },
                            Colour = colourProvider.Highlight1,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = OsuFont.TorusAlternate.With(size: 24),
                            Text = SlopSongSelectStrings.EditorTitle,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = OsuFont.TorusAlternate.With(size: 24),
                            Text = "·",
                            Colour = colourProvider.Highlight1,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = OsuFont.TorusAlternate.With(size: 24),
                            Text = SlopSongSelectStrings.EditorSubtitle,
                            Colour = colourProvider.Highlight1,
                        },
                    }
                };
            }
        }

        /// <summary>
        /// Opens the editor with a new beatmap rather than the beatmap selected at song select, optionally using the given audio track.
        /// </summary>
        private partial class NewBeatmapEditorLoader : EditorLoader
        {
            private readonly FileInfo? audioTrack;

            public NewBeatmapEditorLoader(FileInfo? audioTrack)
            {
                this.audioTrack = audioTrack;
            }

            protected override Editor CreateEditor() => new Editor(this) { InitialAudioTrack = audioTrack };

            protected override void LoadComplete()
            {
                // the beatmap can't be reset at song select as it would immediately select another one while still current,
                // so it's reset through this screen's lease instead, which only happens once song select has been suspended.
                Beatmap.SetDefault();

                base.LoadComplete();
            }
        }

        private partial class FooterButtonNewBeatmap : ScreenFooterButton
        {
            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                Text = SlopSongSelectStrings.NewBeatmap;
                Icon = FontAwesome.Solid.Plus;
                AccentColour = colours.Orange1;
            }
        }
    }
}
