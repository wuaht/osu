// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Changelog;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class ClientChangelogTest
    {
        [Test]
        public void TestEmbeddedChangelog()
        {
            // without git or commits on top of master (e.g. in CI), there is nothing to check.
            if (ClientChangelog.Streams.Count == 0)
                Assert.Ignore("No commits of this client were embedded.");

            var allBuilds = ClientChangelog.Builds;

            Assert.That(allBuilds, Is.Not.Empty);
            Assert.That(allBuilds.Select(b => b.CreatedAt), Is.Ordered.Descending);
            Assert.That(allBuilds.Select(b => b.Id).Distinct().Count(), Is.EqualTo(allBuilds.Count));
            Assert.That(allBuilds.SelectMany(b => b.ChangelogEntries).Select(e => e.Id).Distinct().Count(), Is.EqualTo(allBuilds.Sum(b => b.ChangelogEntries.Count)));
            Assert.That(ClientChangelog.Streams.Select(s => s.Name).Distinct().Count(), Is.EqualTo(ClientChangelog.Streams.Count));

            foreach (var stream in ClientChangelog.Streams)
            {
                var builds = allBuilds.Where(b => b.UpdateStream == stream).ToList();

                Assert.That(builds, Is.Not.Empty);
                Assert.That(stream.Id, Is.Negative);
                Assert.That(stream.LatestBuild, Is.SameAs(builds[0]));
                Assert.That(builds.Select(b => b.CreatedAt.Date).Distinct().Count(), Is.EqualTo(builds.Count));
                Assert.That(APIUpdateStream.KNOWN_STREAMS.ContainsKey(stream.Name));

                for (int i = 0; i < builds.Count; i++)
                {
                    var build = builds[i];

                    Assert.That(ClientChangelog.IsLocal(build));
                    Assert.That(build.Id, Is.Negative);
                    Assert.That(build.ChangelogEntries, Is.Not.Empty);
                    Assert.That(build.ChangelogEntries.All(e => !string.IsNullOrEmpty(e.Title) && !string.IsNullOrEmpty(e.Category)));
                    Assert.That(build.Versions!.Next, Is.SameAs(i > 0 ? builds[i - 1] : null));
                    Assert.That(build.Versions.Previous, Is.SameAs(i < builds.Count - 1 ? builds[i + 1] : null));
                }
            }
        }
    }
}
