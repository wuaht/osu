// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Database;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class OfficialDatabaseCompatibilityTest
    {
        private const string shared_filename = @"client.realm";
        private const string separate_filename = @"client_53.realm";

        private static readonly byte[] shared_content = { 1, 2, 3, 4 };

        private TemporaryNativeStorage storage = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage($"{nameof(OfficialDatabaseCompatibilityTest)}-{Guid.NewGuid()}");
            File.WriteAllBytes(storage.GetFullPath(shared_filename, true), shared_content);
        }

        [TearDown]
        public void TearDown() => storage.Dispose();

        [Test]
        public void TestSameVersionUsesSharedDatabase()
        {
            string filename = OfficialDatabaseCompatibility.GetDatabaseFilename(storage, shared_filename, 53, 53, out bool separate);

            Assert.That(filename, Is.EqualTo(shared_filename));
            Assert.That(separate, Is.False);
            Assert.That(storage.Exists(separate_filename), Is.False);
        }

        [Test]
        public void TestUnknownOfficialVersionUsesSharedDatabase()
        {
            string filename = OfficialDatabaseCompatibility.GetDatabaseFilename(storage, shared_filename, 53, null, out bool separate);

            Assert.That(filename, Is.EqualTo(shared_filename));
            Assert.That(separate, Is.False);
            Assert.That(storage.Exists(separate_filename), Is.False);
        }

        [Test]
        public void TestNewerOfficialVersionUsesSharedDatabase()
        {
            // the shared database being too new is handled by RealmAccess when opening it.
            string filename = OfficialDatabaseCompatibility.GetDatabaseFilename(storage, shared_filename, 53, 54, out bool separate);

            Assert.That(filename, Is.EqualTo(shared_filename));
            Assert.That(separate, Is.False);
        }

        [Test]
        public void TestNewerClientVersionUsesCopyOfSharedDatabase()
        {
            var sharedLastWrite = File.GetLastWriteTimeUtc(storage.GetFullPath(shared_filename));

            string filename = OfficialDatabaseCompatibility.GetDatabaseFilename(storage, shared_filename, 53, 52, out bool separate);

            Assert.That(filename, Is.EqualTo(separate_filename));
            Assert.That(separate, Is.True);
            Assert.That(File.ReadAllBytes(storage.GetFullPath(separate_filename)), Is.EqualTo(shared_content));

            // the shared database must stay untouched.
            Assert.That(File.ReadAllBytes(storage.GetFullPath(shared_filename)), Is.EqualTo(shared_content));
            Assert.That(File.GetLastWriteTimeUtc(storage.GetFullPath(shared_filename)), Is.EqualTo(sharedLastWrite));
        }

        [Test]
        public void TestExistingCopyIsReused()
        {
            byte[] copyContent = { 5, 6, 7 };
            File.WriteAllBytes(storage.GetFullPath(separate_filename), copyContent);

            string filename = OfficialDatabaseCompatibility.GetDatabaseFilename(storage, shared_filename, 53, 52, out bool separate);

            Assert.That(filename, Is.EqualTo(separate_filename));
            Assert.That(separate, Is.True);
            Assert.That(File.ReadAllBytes(storage.GetFullPath(separate_filename)), Is.EqualTo(copyContent));
            Assert.That(File.ReadAllBytes(storage.GetFullPath(shared_filename)), Is.EqualTo(shared_content));
        }

        [Test]
        public void TestNewerClientVersionWithoutSharedDatabase()
        {
            storage.Delete(shared_filename);

            string filename = OfficialDatabaseCompatibility.GetDatabaseFilename(storage, shared_filename, 53, 52, out bool separate);

            Assert.That(filename, Is.EqualTo(separate_filename));
            Assert.That(separate, Is.True);

            // realm creates the database on first access, there is nothing to copy from.
            Assert.That(storage.GetFiles(string.Empty).ToArray(), Is.Empty);
        }
    }
}
