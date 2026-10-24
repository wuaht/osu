// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Online.Discord;
using osu.Game.Overlays;
using osu.Game.Overlays.Discord;
using osu.Game.Tests.Visual;
using osuTK.Graphics;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneDiscordOverlay : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(DiscordStyle.HUE);

        private TestDiscordClient client = null!;
        private DiscordMainView mainView = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create main view", () =>
            {
                client = new TestDiscordClient();

                Child = new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.Both,
                    Height = 0.7f,
                    Children = new Drawable[]
                    {
                        client,
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black,
                            Alpha = 0.6f,
                        },
                        mainView = new DiscordMainView(client),
                    },
                };
            });

            AddUntilStep("wait for messages", () => mainView.ChildrenOfType<DrawableDiscordMessage>().Any());
        }

        [Test]
        public void TestGuildChannel()
        {
            AddAssert("guild selected", () => mainView.ChildrenOfType<DrawableDiscordMessage>().First().Message.ChannelId == TestDiscordClient.GENERAL_CHANNEL);
        }

        [Test]
        public void TestSendMessage()
        {
            AddStep("receive message", () => client.Receive(TestDiscordClient.GENERAL_CHANNEL, "new message at 01:23:456 (1,2,3) - with **bold** text"));
            AddUntilStep("message shown", () => mainView.ChildrenOfType<DrawableDiscordMessage>().Any(m => m.Message.Content.StartsWith("new message", StringComparison.Ordinal)));

            AddStep("delete message", () => client.DeleteLast(TestDiscordClient.GENERAL_CHANNEL));
            AddUntilStep("message removed", () => mainView.ChildrenOfType<DrawableDiscordMessage>().All(m => !m.Message.Content.StartsWith("new message", StringComparison.Ordinal)));
        }

        [Test]
        public void TestDirectMessages()
        {
            AddStep("open direct messages", () => mainView.ChildrenOfType<DiscordGuildRail>().Single().ChildrenOfType<OsuClickableContainer>().First().TriggerClick());
            AddUntilStep("first direct message opened", () => mainView.ChildrenOfType<DrawableDiscordMessage>().Any(m => m.Message.ChannelId == TestDiscordClient.DM_CHANNEL));
        }

        /// <summary>
        /// A client with a guild and direct messages of made up users.
        /// </summary>
        private partial class TestDiscordClient : DiscordClient
        {
            public const ulong GENERAL_CHANNEL = 1001;
            public const ulong DM_CHANNEL = 2001;

            public override bool SupportsGuilds => true;
            public override bool SupportsHistory => true;

            private readonly Dictionary<ulong, List<DiscordMessage>> messages = new Dictionary<ulong, List<DiscordMessage>>();

            private readonly DiscordUser localUser = new DiscordUser { Id = 1, Username = "worte", GlobalName = "worte" };
            private readonly DiscordUser peppy = new DiscordUser { Id = 2, Username = "peppy", GlobalName = "peppy" };
            private readonly DiscordUser mapper = new DiscordUser { Id = 3, Username = "mapper", GlobalName = "A Mapper" };
            private readonly DiscordUser bot = new DiscordUser { Id = 4, Username = "owo", GlobalName = "owo", Bot = true };

            private ulong nextId = 10000;

            public TestDiscordClient()
            {
                foreach (var user in new[] { localUser, peppy, mapper, bot })
                    StoreUser(user);

                LocalUser.Value = localUser;

                var guild = new DiscordGuild
                {
                    Id = 100,
                    Name = "osu! mapping",
                    Roles = { new DiscordRole { Id = 500, Name = "Mapper", Colour = 0x66ccff, Position = 1 } },
                    LocalUserRoles = new List<ulong> { 500 },
                };

                guild.Channels.AddRange(new[]
                {
                    new DiscordChannel { Id = 900, GuildId = 100, Type = DiscordChannelType.GuildCategory, Name = "Text Channels", Position = 0 },
                    new DiscordChannel { Id = GENERAL_CHANNEL, GuildId = 100, Type = DiscordChannelType.GuildText, Name = "general", ParentId = 900, Position = 0, Topic = "Talk about mapping. Timestamps like 00:12:345 (1,2) - work here." },
                    new DiscordChannel { Id = 1002, GuildId = 100, Type = DiscordChannelType.GuildText, Name = "mod-requests", ParentId = 900, Position = 1 },
                    new DiscordChannel { Id = 1003, GuildId = 100, Type = DiscordChannelType.GuildAnnouncement, Name = "announcements", ParentId = 900, Position = 2 },
                    new DiscordChannel { Id = 901, GuildId = 100, Type = DiscordChannelType.GuildCategory, Name = "Voice Channels", Position = 1 },
                    new DiscordChannel { Id = 1004, GuildId = 100, Type = DiscordChannelType.GuildVoice, Name = "Lounge", ParentId = 901, Position = 0 },
                });

                Guilds.Add(guild);
                Guilds.Add(new DiscordGuild { Id = 101, Name = "Tournament Staff" });
                Guilds.Add(new DiscordGuild { Id = 102, Name = "Ranked Mapping Hub" });

                PrivateChannels.Add(new DiscordChannel { Id = DM_CHANNEL, Type = DiscordChannelType.DirectMessage, Recipients = { peppy } });
                PrivateChannels.Add(new DiscordChannel { Id = 2002, Type = DiscordChannelType.GroupDirectMessage, Name = "collab", Recipients = { peppy, mapper } });

                var start = DateTimeOffset.Now.AddDays(-1).AddHours(-2);

                messages[GENERAL_CHANNEL] = new List<DiscordMessage>
                {
                    create(GENERAL_CHANNEL, mapper, start, "hey, can someone check my kiai at 01:02:345 (1,2,3) - ?"),
                    create(GENERAL_CHANNEL, mapper, start.AddMinutes(1), "it feels a bit _off_ to me"),
                    create(GENERAL_CHANNEL, peppy, start.AddMinutes(30), "looks fine, maybe reduce the spacing a bit\n> quote of the day: **hitsounds matter**"),
                    create(GENERAL_CHANNEL, bot, DateTimeOffset.Now.AddMinutes(-20), "", e =>
                    {
                        e.Embeds.Add(new DiscordEmbed
                        {
                            Type = "rich",
                            Title = "Beatmap ranked",
                            Url = "https://osu.ppy.sh/beatmapsets/1",
                            Colour = 0xff66aa,
                            Description = "**Some Artist - Some Title** by A Mapper\n`4.21★` Insane",
                            Fields = { new DiscordEmbedField { Name = "BPM", Value = "180" } },
                        });
                    }),
                    create(GENERAL_CHANNEL, localUser, DateTimeOffset.Now.AddMinutes(-5), "thanks <@3>! ||spoiler|| `code` ~~strike~~ :) 🎉"),
                    create(GENERAL_CHANNEL, mapper, DateTimeOffset.Now.AddMinutes(-2), "```\nslider velocity 1.4x\n```\nyou're welcome <t:" + DateTimeOffset.Now.AddHours(2).ToUnixTimeSeconds() + ":R>", m => m.Mentions.Add(localUser)),
                };

                messages[DM_CHANNEL] = new List<DiscordMessage>
                {
                    create(DM_CHANNEL, peppy, DateTimeOffset.Now.AddMinutes(-10), "hi! 👋"),
                };
            }

            private DiscordMessage create(ulong channel, DiscordUser author, DateTimeOffset time, string content, Action<DiscordMessage>? apply = null)
            {
                var message = new DiscordMessage
                {
                    Id = nextId++,
                    ChannelId = channel,
                    GuildId = channel == GENERAL_CHANNEL ? 100 : null,
                    Author = author,
                    Content = content,
                    Timestamp = time,
                };

                apply?.Invoke(message);
                return message;
            }

            public void Receive(ulong channel, string content)
            {
                var message = create(channel, peppy, DateTimeOffset.Now, content);
                messages[channel].Add(message);
                TriggerMessageCreated(message);
            }

            public void DeleteLast(ulong channel)
            {
                var message = messages[channel].Last();
                messages[channel].Remove(message);
                TriggerMessageDeleted(channel, message.Id);
            }

            public override void Login() => State.Value = DiscordConnectionState.Ready;

            public override void Logout() => State.Value = DiscordConnectionState.LoggedOut;

            public override Task<IReadOnlyList<DiscordMessage>> GetMessages(DiscordChannel channel, ulong? before = null)
            {
                IReadOnlyList<DiscordMessage> result = messages.TryGetValue(channel.Id, out var list)
                    ? list.Where(m => before == null || m.Id < before).ToList()
                    : Array.Empty<DiscordMessage>();

                return Task.FromResult(result);
            }

            public override Task<DiscordMessage> SendMessage(DiscordChannel channel, string content, string nonce)
            {
                var message = create(channel.Id, localUser, DateTimeOffset.Now, content);
                message.Nonce = nonce;

                if (!messages.TryGetValue(channel.Id, out var list))
                    messages[channel.Id] = list = new List<DiscordMessage>();

                list.Add(message);
                return Task.FromResult(message);
            }
        }
    }
}
