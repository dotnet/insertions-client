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
        /// <param name="defaultConfigFile">Full path &quot;default.config&quot; file</param>
        /// <returns><see cref="UpdateResults"/> detailing the operation's outcome.</returns>
        UpdateResults UpdateVersions(string manifestFile, string defaultConfigFile);
    }
}