// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("InsertionsClient.Core.Test")]
namespace Microsoft.DotNet.InsertionsClient.Api
{
    /// <summary>
    /// Defines features to manage default.config insertions from manifest.json.
    /// </summary>
    public interface IInsertionApi
    {
        /// <summary>
        /// Updates default.config NuGet package versions from matching manifest.json assets.
        /// </summary>
        /// <param name="manifestFiles">The paths to all the manifest.json files to be inserted.</param>
        /// <param name="defaultConfigFile">Full path to &quot;default.config&quot; file.</param>
        /// <param name="whitelistedPackages">Regex patterns matching with the packages that are allowed to be updated. If the set is empty,
        /// any package is allowed be updated unless specified in packagesToIgnore.</param>
        /// <param name="packagesToIgnore">Set of packages to be ignored.</param>
        /// <param name="accessToken">Access token used when connecting to nuget feed.</param>
        /// <param name="propsFilesRootDirectory">Directory that will be searched for props files.</param>
        /// <param name="buildFilter">Predicate to determine if a build should be included in the insertion process.
        /// If null, all the builds will be included.</param>
        /// <returns><see cref="UpdateResults"/> detailing the operation's outcome.</returns>
        UpdateResults UpdateVersions(
            IEnumerable<string> manifestFiles,
            string defaultConfigFile,
            IEnumerable<Regex> whitelistedPackages,
            ImmutableHashSet<string>? packagesToIgnore,
            string? accessToken,
            string? propsFilesRootDirectory,
            Predicate<Build>? buildFilter);
    }
}