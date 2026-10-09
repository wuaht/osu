// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Users;

namespace osu.Game.Online.OfflineProfiles
{
    /// <summary>
    /// Manages <see cref="OfflineProfile"/>s, which are stored in the data folder.
    /// While not logged in, the active profile is the local user, so that scores are set on it.
    /// </summary>
    public partial class OfflineProfileManager : Component
    {
        private const string directory_name = @"offline-profiles";
        private const string data_filename = @"profiles.json";

        private static readonly string[] supported_image_extensions = { @".png", @".jpg", @".jpeg" };

        /// <summary>
        /// All profiles, in order of creation.
        /// </summary>
        public IBindableList<OfflineProfile> Profiles => profiles;

        private readonly BindableList<OfflineProfile> profiles = new BindableList<OfflineProfile>();

        /// <summary>
        /// The active profile, which is the local user while not logged in. <c>null</c> to play as a guest.
        /// </summary>
        public IBindable<OfflineProfile?> ActiveProfile => activeProfile;

        private readonly Bindable<OfflineProfile?> activeProfile = new Bindable<OfflineProfile?>();

        /// <summary>
        /// Invoked when a profile changes (e.g. its username or images), but not when it is added or removed.
        /// </summary>
        public event Action<OfflineProfile>? ProfileChanged;

        private readonly Storage storage;

        /// <summary>
        /// A copy of <see cref="profiles"/> which can be accessed from any thread.
        /// </summary>
        private volatile OfflineProfile[] profilesSnapshot = Array.Empty<OfflineProfile>();

        private int nextUserID = OfflineProfile.FIRST_USER_ID;

        private LargeTextureStore textures = null!;

        private Bindable<string> activeProfileSetting = null!;

        private Bindable<bool> includeUnrankedBeatmaps = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        /// <summary>
        /// The calculated performance of scores by their ID, as local scores don't store their performance.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, double> performanceCache = new ConcurrentDictionary<Guid, double>();

        public OfflineProfileManager(Storage storage)
        {
            this.storage = storage.GetStorageForDirectory(directory_name);
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host, OsuConfigManager config)
        {
            textures = new LargeTextureStore(host.Renderer, host.CreateTextureLoaderStore(new StorageBackedResourceStore(storage)));

            loadData();

            includeUnrankedBeatmaps = config.GetBindable<bool>(OsuSetting.SlopOfflineProfilesIncludeUnranked);

            activeProfileSetting = config.GetBindable<string>(OsuSetting.SlopActiveOfflineProfile);
            activeProfileSetting.BindValueChanged(id =>
            {
                activeProfile.Value = Guid.TryParse(id.NewValue, out var guid) ? profiles.FirstOrDefault(p => p.ID == guid) : null;
            }, true);

            activeProfile.BindValueChanged(_ => updateLocalUser(), true);
        }

        #region Managing profiles

        /// <summary>
        /// Creates a new profile.
        /// </summary>
        /// <param name="username">The username of the profile.</param>
        /// <param name="error">The reason why the profile couldn't be created.</param>
        /// <returns>The created profile, or <c>null</c> if the username is invalid.</returns>
        public OfflineProfile? Create(string username, out string? error)
        {
            username = username.Trim();
            error = validateUsername(username, null);

            if (error != null)
                return null;

            var profile = new OfflineProfile
            {
                UserID = nextUserID--,
                Username = username,
                JoinDate = DateTimeOffset.Now,
            };

            profiles.Add(profile);
            save();

            return profile;
        }

        /// <summary>
        /// Changes the username of a profile. Existing scores keep the previous username.
        /// </summary>
        /// <returns>The reason why the username couldn't be changed, or <c>null</c> if it was changed.</returns>
        public string? Rename(OfflineProfile profile, string username)
        {
            username = username.Trim();

            string? error = validateUsername(username, profile);

            if (error != null)
                return error;

            profile.Username = username;
            onProfileChanged(profile);

            return null;
        }

        /// <summary>
        /// Changes the country of a profile.
        /// </summary>
        public void SetCountry(OfflineProfile profile, CountryCode countryCode)
        {
            profile.CountryCode = countryCode;
            onProfileChanged(profile);
        }

        /// <summary>
        /// Deletes a profile along with its images. Scores set on it are kept.
        /// </summary>
        public void Delete(OfflineProfile profile)
        {
            if (activeProfile.Value == profile)
                SetActiveProfile(null);

            profiles.Remove(profile);
            save();

            try
            {
                storage.DeleteDirectory(profile.ID.ToString());
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to delete the files of offline profile {profile.Username}");
            }
        }

        /// <summary>
        /// Sets the profile which is the local user while not logged in.
        /// </summary>
        /// <param name="profile">The profile, or <c>null</c> to play as a guest.</param>
        public void SetActiveProfile(OfflineProfile? profile) => activeProfileSetting.Value = profile?.ID.ToString() ?? string.Empty;

        private string? validateUsername(string username, OfflineProfile? existing)
        {
            if (string.IsNullOrEmpty(username))
                return "The username can't be empty.";

            if (username.Length > OfflineProfile.MAX_USERNAME_LENGTH)
                return $"The username can't be longer than {OfflineProfile.MAX_USERNAME_LENGTH} characters.";

            if (username.Any(char.IsControl))
                return "The username contains invalid characters.";

            if (profiles.Any(p => p != existing && string.Equals(p.Username, username, StringComparison.OrdinalIgnoreCase)))
                return "A profile with this username already exists.";

            return null;
        }

        #endregion

        #region Images

        /// <summary>
        /// Whether the file is an image which can be used as an avatar or cover.
        /// </summary>
        public static bool IsSupportedImage(string path) => supported_image_extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

        /// <summary>
        /// Sets the avatar of a profile to a copy of the given image file.
        /// </summary>
        public void SetAvatar(OfflineProfile profile, string imagePath) => setImage(profile, imagePath, @"avatar", (p, filename) => p.AvatarFilename = filename, p => p.AvatarFilename);

        /// <summary>
        /// Sets the cover (banner) of a profile to a copy of the given image file.
        /// </summary>
        public void SetCover(OfflineProfile profile, string imagePath) => setImage(profile, imagePath, @"cover", (p, filename) => p.CoverFilename = filename, p => p.CoverFilename);

        public void ClearAvatar(OfflineProfile profile) => clearImage(profile, (p, filename) => p.AvatarFilename = filename, p => p.AvatarFilename);

        public void ClearCover(OfflineProfile profile) => clearImage(profile, (p, filename) => p.CoverFilename = filename, p => p.CoverFilename);

        private void setImage(OfflineProfile profile, string imagePath, string name, Action<OfflineProfile, string?> setFilename, Func<OfflineProfile, string?> getFilename)
        {
            if (!IsSupportedImage(imagePath))
                throw new ArgumentException($"Unsupported image format: {imagePath}", nameof(imagePath));

            // a new filename each time, as textures are cached by their filename.
            string filename = $"{name}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{Path.GetExtension(imagePath).ToLowerInvariant()}";

            using (var source = File.OpenRead(imagePath))
            using (var destination = storage.CreateFileSafely(Path.Combine(profile.ID.ToString(), filename)))
                source.CopyTo(destination);

            string? previous = getFilename(profile);

            setFilename(profile, filename);
            onProfileChanged(profile);

            deleteImageFile(profile, previous);
        }

        private void clearImage(OfflineProfile profile, Action<OfflineProfile, string?> setFilename, Func<OfflineProfile, string?> getFilename)
        {
            string? previous = getFilename(profile);

            if (previous == null)
                return;

            setFilename(profile, null);
            onProfileChanged(profile);

            deleteImageFile(profile, previous);
        }

        private void deleteImageFile(OfflineProfile profile, string? filename)
        {
            if (filename == null)
                return;

            try
            {
                storage.Delete(Path.Combine(profile.ID.ToString(), filename));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to delete an image of offline profile {profile.Username}");
            }
        }

        /// <summary>
        /// Returns the full path of the avatar file of a profile, if set.
        /// </summary>
        public string? GetAvatarPath(OfflineProfile profile) => profile.AvatarFilename == null ? null : storage.GetFullPath(Path.Combine(profile.ID.ToString(), profile.AvatarFilename));

        /// <summary>
        /// Returns the full path of the cover file of a profile, if set.
        /// </summary>
        public string? GetCoverPath(OfflineProfile profile) => profile.CoverFilename == null ? null : storage.GetFullPath(Path.Combine(profile.ID.ToString(), profile.CoverFilename));

        /// <summary>
        /// Returns the avatar of the offline profile with the given user ID. Thread-safe.
        /// </summary>
        /// <returns>The avatar, or <c>null</c> if the profile doesn't exist or has no avatar.</returns>
        public Texture? GetAvatar(int userId) => getImage(userId, p => p.AvatarFilename);

        /// <summary>
        /// Returns the cover of the offline profile with the given user ID. Thread-safe.
        /// </summary>
        /// <returns>The cover, or <c>null</c> if the profile doesn't exist or has no cover.</returns>
        public Texture? GetCover(int userId) => getImage(userId, p => p.CoverFilename);

        private Texture? getImage(int userId, Func<OfflineProfile, string?> getFilename)
        {
            var profile = GetProfileByUserID(userId);
            string? filename = profile == null ? null : getFilename(profile);

            if (profile == null || filename == null)
                return null;

            // resource stores use forward slashes.
            return textures.Get($"{profile.ID}/{filename}");
        }

        #endregion

        /// <summary>
        /// Calculates the statistics of all profiles in a ruleset from their local scores, and ranks the profiles by them.
        /// Must be called from the update thread.
        /// </summary>
        public Task<OfflineProfileRanking> CalculateRankingAsync(RulesetInfo ruleset, CancellationToken cancellationToken = default)
        {
            bool includeUnranked = includeUnrankedBeatmaps.Value;

            // captured on the update thread, as profiles are changed there.
            var profileData = profiles.Select(profile => (profile, playCount: profile.PlayCounts.GetValueOrDefault(ruleset.ShortName))).ToList();

            var scoresByProfile = realm.Run(r => profileData.ToDictionary(p => p.profile, p => r.GetAllLocalScoresForUser(p.profile.UserID)
                                                                                         .AsEnumerable()
                                                                                         .Where(s => s.Ruleset.ShortName == ruleset.ShortName)
                                                                                         .Select(s => s.Detach())
                                                                                         .ToList()));

            return Task.Run(async () =>
            {
                var entries = new List<OfflineProfileRanking.Entry>();

                foreach (var (profile, playCount) in profileData)
                {
                    var statistics = await OfflineProfileStatistics.CalculateAsync(scoresByProfile[profile], ruleset, playCount, difficultyCache, performanceCache,
                        includeUnranked, cancellationToken).ConfigureAwait(false);

                    entries.Add(new OfflineProfileRanking.Entry(profile, statistics));
                }

                return new OfflineProfileRanking(entries);
            }, cancellationToken);
        }

        /// <summary>
        /// Returns the profile with the given user ID. Thread-safe.
        /// </summary>
        public OfflineProfile? GetProfileByUserID(int userId) => profilesSnapshot.FirstOrDefault(p => p.UserID == userId);

        /// <summary>
        /// Counts a started play for the active profile, if it is the local user.
        /// </summary>
        public void RecordPlay(IRulesetInfo ruleset)
        {
            if (api.LocalUser.Value is not OfflineProfileUser user)
                return;

            var profile = profiles.FirstOrDefault(p => p.ID == user.ProfileID);

            if (profile == null)
                return;

            profile.PlayCounts[ruleset.ShortName] = profile.PlayCounts.GetValueOrDefault(ruleset.ShortName) + 1;
            onProfileChanged(profile);
        }

        private void onProfileChanged(OfflineProfile profile)
        {
            save();

            if (activeProfile.Value == profile)
                updateLocalUser();

            ProfileChanged?.Invoke(profile);
        }

        private void updateLocalUser() => api.LocalUserState.SetOfflineProfileUser(activeProfile.Value?.CreateUser());

        #region Storage

        private class StoredData
        {
            public int NextUserID { get; set; } = OfflineProfile.FIRST_USER_ID;

            public List<OfflineProfile> Profiles { get; set; } = new List<OfflineProfile>();
        }

        private void loadData()
        {
            try
            {
                if (storage.Exists(data_filename))
                {
                    using (var stream = storage.GetStream(data_filename))
                    using (var reader = new StreamReader(stream))
                    {
                        var data = JsonConvert.DeserializeObject<StoredData>(reader.ReadToEnd());

                        if (data != null)
                        {
                            // never reuse IDs, even if the stored next ID is inconsistent with the profiles.
                            nextUserID = Math.Min(data.NextUserID, data.Profiles.Select(p => p.UserID - 1).DefaultIfEmpty(OfflineProfile.FIRST_USER_ID).Min());
                            profiles.AddRange(data.Profiles.Where(p => OfflineProfileUser.IsOfflineProfileID(p.UserID)));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to load offline profiles");
            }

            profilesSnapshot = profiles.ToArray();
        }

        private void save()
        {
            profilesSnapshot = profiles.ToArray();

            var data = new StoredData
            {
                NextUserID = nextUserID,
                Profiles = profiles.ToList(),
            };

            try
            {
                using (var stream = storage.CreateFileSafely(data_filename))
                using (var writer = new StreamWriter(stream))
                    writer.Write(JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to save offline profiles");
            }
        }

        #endregion
    }
}
