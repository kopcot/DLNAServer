namespace DlnaServer.Host.Configuration
{
    /// <summary>
    /// Registers <c>config.json</c> as a configuration source: fatal if it cannot be read at startup,
    /// survivable if it stops being readable later.
    /// </summary>
    /// <remarks>
    /// A type of its own because getting either half wrong is silent. Both halves have been wrong once,
    /// in the same change, and the second hid the first: the server came up on code defaults - the
    /// wrong port, the wrong library, a cache budget four times the configured one - and logged nothing
    /// but a source-folder fallback warning.
    /// </remarks>
    internal static class DlnaConfigurationFile
    {
        /// <summary>
        /// Adds the file at <paramref name="path"/>, calling <paramref name="onReloadFailure"/> when a
        /// later re-read fails and <paramref name="onValuesRetained"/> when the previous values were
        /// kept in its place.
        /// </summary>
        /// <remarks>
        /// <paramref name="builder"/> must load eagerly - a <c>ConfigurationManager</c>, which is what
        /// <c>WebApplicationBuilder.Configuration</c> is. The initial-load guard below depends on it:
        /// a deferred builder would not read the file until <c>Build()</c>, by which point the flag has
        /// already been set and an initial failure would be treated as a reload.
        /// <para>
        /// The source is a <see cref="LastGoodJsonConfigurationSource"/> rather than a plain JSON one
        /// because <c>context.Ignore</c> alone loses every value - see that type for the measurement
        /// and for what the loss cost.
        /// </para>
        /// </remarks>
        public static void Add(
            IConfigurationBuilder builder,
            string path,
            Action<Exception> onReloadFailure,
            Action<string> onValuesRetained)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentNullException.ThrowIfNull(onReloadFailure);
            ArgumentNullException.ThrowIfNull(onValuesRetained);

            var initialLoadCompleted = false;

            _ = builder.Add<LastGoodJsonConfigurationSource>(source =>
            {
                source.Path = path;
                source.Optional = false;
                source.ReloadOnChange = true;
                source.OnValuesRetained = onValuesRetained;

                // ResolveFileProvider is the whole reason this is not the two-line convenience overload
                // written out longhand. AddJsonFile(path, optional, reloadOnChange) calls it; the
                // Action<JsonConfigurationSource> overload does NOT. It is what splits an ABSOLUTE path
                // into a PhysicalFileProvider over its directory plus a bare file name - and
                // PhysicalFileProvider refuses a rooted path outright, so without this the file is
                // silently "not found" while the path in the source looks perfectly correct.
                source.ResolveFileProvider();

                source.OnLoadException = context =>
                {
                    if (!initialLoadCompleted)
                    {
                        // Left to throw. ConfigurationFileGuard has just replaced an unreadable file
                        // with defaults, so a failure at this point is real, and ignoring it is how the
                        // server booted on 26851 - the reference implementation's port - serving its own
                        // publish folder. A configuration file that cannot be read is not survivable;
                        // one that stops being readable later is.
                        return;
                    }

                    // Ignore suppresses the rethrow and nothing else - the provider has already emptied
                    // its data by this point, so LoadFailed is what tells it to put the previous values
                    // back. Without that, ignoring the failure is how a bad edit reached the indexer.
                    context.Ignore = true;
                    source.LoadFailed = true;
                    onReloadFailure(context.Exception);
                };
            });

            // Reached only once the first load has succeeded, because the builder loads eagerly. An
            // initial failure throws out of AddJsonFile above and never gets here.
            initialLoadCompleted = true;
        }
    }
}
