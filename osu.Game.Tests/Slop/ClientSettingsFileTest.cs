// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Testing;
using osu.Game.Configuration;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class ClientSettingsFileTest
    {
        private const string shared_filename = @"game.ini";
        private const string client_filename = OsuConfigManager.CLIENT_SETTINGS_PREFIX + shared_filename;

        private TemporaryNativeStorage storage = null!;

        [SetUp]
        public void SetUp() => storage = new TemporaryNativeStorage($"{nameof(ClientSettingsFileTest)}-{Guid.NewGuid()}");

        [TearDown]
        public void TearDown() => storage.Dispose();

        [Test]
        public void TestSharedSettingsAreCopiedOnFirstStart()
        {
            write(shared_filename, $"{OsuSetting.ShowFpsDisplay} = True");

            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<bool>(OsuSetting.ShowFpsDisplay), Is.True);
                Assert.That(config.Save(), Is.True);
            }

            Assert.That(read(client_filename), Does.Contain($"{OsuSetting.ShowFpsDisplay} = True"));
        }

        [Test]
        public void TestSettingsAreNotSavedToSharedFile()
        {
            write(shared_filename, $"{OsuSetting.ShowFpsDisplay} = False");

            using (var config = new OsuConfigManager(storage))
            {
                config.SetValue(OsuSetting.SlopEditorBlanketSnap, false);
                config.SetValue(OsuSetting.ShowFpsDisplay, true);
                Assert.That(config.Save(), Is.True);
            }

            Assert.That(read(shared_filename), Is.EqualTo($"{OsuSetting.ShowFpsDisplay} = False"));

            string client = read(client_filename);
            Assert.That(client, Does.Contain($"{OsuSetting.SlopEditorBlanketSnap} = False"));
            Assert.That(client, Does.Contain($"{OsuSetting.ShowFpsDisplay} = True"));
        }

        [Test]
        public void TestSharedSettingsAreIgnoredAfterFirstStart()
        {
            using (var config = new OsuConfigManager(storage))
                config.SetValue(OsuSetting.SlopEditorBlanketSnap, false);

            // the official release changes its own settings afterwards.
            write(shared_filename, $"{OsuSetting.ShowFpsDisplay} = True");

            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<bool>(OsuSetting.ShowFpsDisplay), Is.False);
                Assert.That(config.Get<bool>(OsuSetting.SlopEditorBlanketSnap), Is.False);
            }
        }

        [Test]
        public void TestDevelopmentConfigUsesSeparateFiles()
        {
            using (var config = new DevelopmentOsuConfigManager(storage))
            {
                config.SetValue(OsuSetting.SlopEditorBlanketSnap, false);
                Assert.That(config.Save(), Is.True);
            }

            Assert.That(storage.Exists("slop.game.dev.ini"), Is.True);
            Assert.That(storage.Exists("game.dev.ini"), Is.False);
            Assert.That(storage.Exists(client_filename), Is.False);
        }

        [Test]
        public void TestFrameworkSettingsAreCopiedOnFirstStart()
        {
            write("framework.ini", $"{FrameworkSetting.VolumeMusic} = 0.5");

            using (var config = new FrameworkConfigManager(storage, filenamePrefix: OsuConfigManager.CLIENT_SETTINGS_PREFIX))
            {
                Assert.That(config.Get<double>(FrameworkSetting.VolumeMusic), Is.EqualTo(0.5));

                config.SetValue(FrameworkSetting.VolumeMusic, 0.25);
                Assert.That(config.Save(), Is.True);
            }

            Assert.That(read("framework.ini"), Is.EqualTo($"{FrameworkSetting.VolumeMusic} = 0.5"));
            Assert.That(read("slop.framework.ini"), Does.Contain($"{FrameworkSetting.VolumeMusic} = 0.25"));

            write("framework.ini", $"{FrameworkSetting.VolumeMusic} = 1");

            using (var config = new FrameworkConfigManager(storage, filenamePrefix: OsuConfigManager.CLIENT_SETTINGS_PREFIX))
                Assert.That(config.Get<double>(FrameworkSetting.VolumeMusic), Is.EqualTo(0.25));
        }

        private string read(string filename) => File.ReadAllText(storage.GetFullPath(filename)).TrimEnd();

        private void write(string filename, string content) => File.WriteAllText(storage.GetFullPath(filename, true), content);
    }
}
