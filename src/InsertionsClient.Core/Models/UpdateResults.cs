// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Props.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.DotNet.InsertionsClient.Models
{
    /// <summary>
    /// Describes the response to <see cref="IInsertionApi.UpdateVersions(string, string, string)"/> calls.
    /// </summary>
    public sealed class UpdateResults
    {
        private readonly ConcurrentBag<PackageUpdateResult> _updatedPackages = new ConcurrentBag<PackageUpdateResult>();

        /// <summary>
        /// True if the <see cref="IInsertionApi.UpdateVersions(string, string, string)"/> attempt completed, false otherwise.
        /// </summary>
        public bool Outcome => string.IsNullOrWhiteSpace(OutcomeDetails);

        /// <summary>
        /// Updated default.config NuGet package versions.
        /// </summary>
        public IEnumerable<PackageUpdateResult> UpdatedPackages => _updatedPackages;

        /// <summary>
        /// Ids of ignored nuget packages.
        /// </summary>
        public ImmutableHashSet<string>? IgnoredNuGets { get; set; }

        /// <summary>
        /// Duration in ms of <see cref="IInsertionApi.UpdateVersions(string, string, string)"/> attempt.
        /// </summary>
        public float DurationMilliseconds { get; set; }

        /// <summary>
        /// Describes the outcome of failed <see cref="IInsertionApi.UpdateVersions(string, string, string)"/> attempts.
        /// </summary>
        public string? OutcomeDetails { get; set; }

        /// <summary>
        /// All the files that were modified during the update or
        /// the exceptions if there was an error while saving the file.
        /// </summary>
        public FileSaveResult[]? FileSaveResults { get; set; }

        /// <summary>
        /// Results of the props file update stage. Null, if stage didn't run.
        /// </summary>
        public PropsUpdateResults? PropsFileUpdateResults { get; set; }

        /// <summary>
        /// Adds the given package to the updated packages list.
        /// </summary>
        internal void AddPackage(PackageUpdateResult packageUpdateResult)
        {
            _updatedPackages.Add(packageUpdateResult);
        }

        public override string ToString()
        {
            return $"Validation {(Outcome ? "succeeded" : "failed")} with {UpdatedPackages.Count()} matched assets ({DurationMilliseconds:N0}-ms)";
        }
    }
}