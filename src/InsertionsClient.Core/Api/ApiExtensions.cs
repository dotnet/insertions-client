// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.InsertionsClient.Api
{
    public static class ApiExtensions
    {
        /// <summary>
        /// Convenience method to call <see cref="IInsertionApi.UpdateVersions(IEnumerable{string}, string, IEnumerable{Regex}, ImmutableHashSet{string}?, string?, string?)"/>
        /// with a single manifest file instead of a set of manifest files.
        /// See the documentation of the related method for the detailed usage.
        /// </summary>
        public static UpdateResults UpdateVersions(this IInsertionApi api,
            string manifestFile,
            string defaultConfigFile,
            IEnumerable<Regex> whitelistedPackages,
            ImmutableHashSet<string>? packagesToIgnore,
            string? accessToken,
            string? propsFilesRootDirectory)
        {
            return api.UpdateVersions(
                new[] { manifestFile },
                defaultConfigFile,
                whitelistedPackages,
                packagesToIgnore,
                accessToken,
                propsFilesRootDirectory);
        }
    }
}
