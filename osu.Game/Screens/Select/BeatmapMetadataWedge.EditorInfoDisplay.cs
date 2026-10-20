// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.Chat;
using osuTK;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapMetadataWedge
    {
        /// <summary>
        /// Displays information about the selected beatmap which is relevant for editing it.
        /// </summary>
        public partial class EditorInfoDisplay : CompositeDrawable
        {
            private MetadataDisplay mappers = null!;
            private MetadataDisplay status = null!;
            private MetadataDisplay addedLocally = null!;
            private MetadataDisplay lastLocalUpdate = null!;
            private MetadataDisplay lastOnlineUpdate = null!;
            private MetadataDisplay onlineIds = null!;

            private BeatmapOwnerLookup ownerLookup = null!;

            [Resolved]
            private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

            [Resolved]
            private ILinkHandler? linkHandler { get; set; }

            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                InternalChildren = new Drawable[]
                {
                    ownerLookup = new BeatmapOwnerLookup(),
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(),
                            new Dimension(),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                createColumn(
                                    mappers = new MetadataDisplay(SlopSongSelectStrings.Mappers),
                                    status = new MetadataDisplay(SongSelectStrings.RankedStatus)),
                                createColumn(
                                    addedLocally = new MetadataDisplay(SlopSongSelectStrings.AddedLocally),
                                    lastLocalUpdate = new MetadataDisplay(SlopSongSelectStrings.LastLocalUpdate)),
                                createColumn(
                                    lastOnlineUpdate = new MetadataDisplay(SlopSongSelectStrings.LastOnlineUpdate),
                                    onlineIds = new MetadataDisplay(SlopSongSelectStrings.OnlineIds)),
                            },
                        },
                    },
                };
            }

            private static FillFlowContainer createColumn(params MetadataDisplay[] displays) => new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0f, 10f),
                Children = displays,
            };

            protected override void LoadComplete()
            {
                base.LoadComplete();

                beatmap.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay), true);
                ownerLookup.Owners.BindValueChanged(_ => Scheduler.AddOnce(updateMappers));
            }

            private void updateDisplay()
            {
                var beatmapInfo = beatmap.Value.BeatmapInfo;
                var beatmapSetInfo = beatmap.Value.BeatmapSetInfo;

                ownerLookup.BeatmapOnlineID = beatmapInfo.OnlineID;
                updateMappers();

                status.Data = (beatmapInfo.Status.GetLocalisableDescription(), null);
                addedLocally.Date = beatmapSetInfo.DateAdded;
                lastLocalUpdate.Date = beatmapInfo.LastLocalUpdate;
                lastOnlineUpdate.Date = beatmapInfo.LastOnlineUpdate;

                if (beatmapInfo.OnlineID > 0)
                    onlineIds.Data = (SlopSongSelectStrings.OnlineIdsValue(beatmapInfo.OnlineID, beatmapSetInfo.OnlineID), null);
                else
                    onlineIds.Data = ("-", null);
            }

            private void updateMappers()
            {
                var owners = ownerLookup.Owners.Value;

                if (owners != null)
                {
                    mappers.Data = (BeatmapOwnerLookup.FormatUsernames(owners), null);
                    return;
                }

                // the locally stored author is the beatmap set host, which is only correct as long as no guest difficulty owners are known.
                var author = beatmap.Value.Metadata.Author;
                mappers.Data = (author.Username, () => linkHandler?.HandleLink(new LinkDetails(LinkAction.OpenUserProfile, author)));
            }
        }
    }
}
