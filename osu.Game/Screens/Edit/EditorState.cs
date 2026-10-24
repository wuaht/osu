// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Structure used to convey the general state of an <see cref="Editor"/> instance.
    /// </summary>
    public class EditorState
    {
        /// <summary>
        /// The current editor mode.
        /// </summary>
        public EditorScreenMode Mode { get; set; }

        /// <summary>
        /// The current audio time.
        /// </summary>
        public double Time { get; set; }

        /// <summary>
        /// The editor clipboard content.
        /// </summary>
        public string ClipboardContent { get; set; } = string.Empty;

        /// <summary>
        /// The playback speed selected by the user.
        /// </summary>
        public double PlaybackSpeed { get; set; } = 1;

        /// <summary>
        /// An editor timestamp (e.g. "00:12:345 (1,2) -") to seek to and select in the compose screen once the editor is ready.
        /// </summary>
        public string? PendingTimestamp { get; set; }
    }
}
