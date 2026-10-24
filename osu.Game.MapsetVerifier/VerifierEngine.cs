// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using MapsetVerifier.Framework;
using MapsetVerifier.Snapshots;

namespace osu.Game.MapsetVerifier
{
    /// <summary>
    /// Sets up the vendored Mapset Verifier engine, and runs work on it the way it expects.
    /// </summary>
    public static class VerifierEngine
    {
        /// <summary>
        /// The folder of the Mapset Verifier app, such that snapshots and check run history are shared with it.
        /// </summary>
        private const string externals_folder_name = @"Mapset Verifier Externals";

        private static readonly object initialise_lock = new object();

        private static bool initialised;

        /// <summary>
        /// Runs a function on the engine, after making sure that the engine is set up.
        /// The engine parses and formats numbers, so it always has to run with the invariant culture.
        /// </summary>
        public static T Run<T>(Func<T> func)
        {
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUICulture = CultureInfo.CurrentUICulture;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

                ensureInitialised();
                return func();
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUICulture;
            }
        }

        /// <summary>
        /// Runs a function on the engine on a background thread.
        /// </summary>
        public static Task<T> RunAsync<T>(Func<T> func, CancellationToken cancellationToken = default) => Task.Factory.StartNew(() => Run(func), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        private static void ensureInitialised()
        {
            lock (initialise_lock)
            {
                if (initialised)
                    return;

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                Checker.ConfigureCustomChecksPath(appData, externals_folder_name);
                Snapshotter.ConfigurePath(appData, externals_folder_name);

                // only the checks shipping with Mapset Verifier run, as plugins could run arbitrary code.
                Checker.DisableCustomChecks();
                Checker.LoadDefaultChecks();

                initialised = true;
            }
        }
    }
}
