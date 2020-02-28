// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Models;

namespace Microsoft.Net.Insertions.Api
{
    /// <summary>
    /// Defines features to manage default.config insertions from manifest.json.
    /// </summary>
    public interface IInsertionApi
    {
        /// <summary>
        /// Updates default.config NuGet package versions from matching manifest.json assets.
        /// </summary>
        /// <param name="manifestFile">Specified manifest.json</param>
        /// <param name="defaultConfigFile">Full path to &quot;default.config&quot; file</param>
        /// <param name="ignoredPackagesFile">Full path to the file which lists all the packages to ignore line by line.
        /// Package ids should be given identical to how they are written in &quot;default.config&quot;.</param>
        /// <returns><see cref="UpdateResults"/> detailing the operation's outcome.</returns>
        UpdateResults UpdateVersions(string manifestFile, string defaultConfigFile, string ignoredPackagesFile);
    }
}