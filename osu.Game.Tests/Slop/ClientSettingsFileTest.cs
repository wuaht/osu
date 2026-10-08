// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class ClientSettingsFileTest
    {
        private const string shared_filename = @"game.ini";

        private TemporaryNativeStorage storage = null!;

        [SetUp]
        public void SetUp() => storage = new TemporaryNativeStorage($"{nameof(ClientSettingsFileTest)}-{Guid.NewGuid()}");

        [TearDown]
        public void TearDown() => storage.Dispose();

        [Test]
        public void TestClientSettingsAreSavedSeparately()
        {
            using (var config = new OsuConfigManager(storage))
            {
                config.SetValue(OsuSetting.SlopEditorBlanketSnap, false);
                config.SetValue(OsuSetting.ShowFpsDisplay, true);
                Assert.That(config.Save(), Is.True);
            }

            string shared = read(shared_filename);
            string client = read(OsuConfigManager.CLIENT_SETTINGS_FILENAME);

            Assert.That(shared, Does.Contain($"{OsuSetting.ShowFpsDisplay} = True"));
            Assert.That(shared, Does.Not.Contain("Slop"));

            Assert.That(client, Does.Contain($"{OsuSetting.SlopEditorBlanketSnap} = False"));
            Assert.That(client, Does.Not.Contain(OsuSetting.ShowFpsDisplay.ToString()));
        }

        [Test]
        public void TestSettingsRoundTrip()
        {
            using (var config = new OsuConfigManager(storage))
            {
                config.SetValue(OsuSetting.SlopEditorBlanketSnap, false);
                config.SetValue(OsuSetting.ShowFpsDisplay, true);
            }

            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<bool>(OsuSetting.SlopEditorBlanketSnap), Is.False);
                Assert.That(config.Get<bool>(OsuSetting.ShowFpsDisplay), Is.True);
            }
        }

        [Test]
        public void TestClientSettingsSurviveSharedFileBeingRewritten()
        {
            using (var config = new OsuConfigManager(storage))
                config.SetValue(OsuSetting.SlopEditorBlanketSnap, false);

            // the official release only writes the settings it knows about.
            write(shared_filename, $"{OsuSetting.ShowFpsDisplay} = True");

            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<bool>(OsuSetting.SlopEditorBlanketSnap), Is.False);
                Assert.That(config.Get<bool>(OsuSetting.ShowFpsDisplay), Is.True);
            }
        }

        [Test]
        public void TestClientSettingsAreMovedOutOfSharedFile()
        {
            // client settings were previously stored in game.ini.
            write(shared_filename, $"{OsuSetting.SlopEditorBlanketSnap} = False\n{OsuSetting.ShowFpsDisplay} = True");

            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<bool>(OsuSetting.SlopEditorBlanketSnap), Is.False);
                Assert.That(config.Save(), Is.True);
            }

            Assert.That(read(shared_filename), Does.Not.Contain("Slop"));
            Assert.That(read(shared_filename), Does.Contain($"{OsuSetting.ShowFpsDisplay} = True"));
            Assert.That(read(OsuConfigManager.CLIENT_SETTINGS_FILENAME), Does.Contain($"{OsuSetting.SlopEditorBlanketSnap} = False"));
        }

        [Test]
        public void TestClientSettingsFileTakesPrecedence()
        {
            write(shared_filename, $"{OsuSetting.SlopEditorBlanketSnap} = False");
            write(OsuConfigManager.CLIENT_SETTINGS_FILENAME, $"{OsuSetting.SlopEditorBlanketSnap} = True");

            using (var config = new OsuConfigManager(storage))
                Assert.That(config.Get<bool>(OsuSetting.SlopEditorBlanketSnap), Is.True);
        }

        [Test]
        public void TestDevelopmentConfigUsesSeparateClientSettingsFile()
        {
            using (var config = new DevelopmentOsuConfigManager(storage))
            {
                config.SetValue(OsuSetting.SlopEditorBlanketSnap, false);
                Assert.That(config.Save(), Is.True);
            }

            Assert.That(storage.Exists("slop.dev.ini"), Is.True);
            Assert.That(storage.Exists(OsuConfigManager.CLIENT_SETTINGS_FILENAME), Is.False);
            Assert.That(read("game.dev.ini"), Does.Not.Contain("Slop"));
        }

        private string read(string filename) => File.ReadAllText(storage.GetFullPath(filename));

        private void write(string filename, string content) => File.WriteAllText(storage.GetFullPath(filename, true), content);
    }
}
