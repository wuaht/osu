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
            if (ClientChangelog.Stream == null)
                Assert.Ignore("No commits of this client were embedded.");

            var builds = ClientChangelog.Builds;

            Assert.That(builds, Is.Not.Empty);
            Assert.That(ClientChangelog.Stream!.LatestBuild, Is.SameAs(builds[0]));
            Assert.That(builds.Select(b => b.CreatedAt), Is.Ordered.Descending);
            Assert.That(builds.Select(b => b.CreatedAt.Date).Distinct().Count(), Is.EqualTo(builds.Count));
            Assert.That(APIUpdateStream.KNOWN_STREAMS.ContainsKey(ClientChangelog.STREAM_NAME));

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
