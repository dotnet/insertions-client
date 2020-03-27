// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Net.Insertions.Models
{
    /// <summary>
    /// Describes the response to <see cref="IInsertionApi.UpdateVersions(string, string, string)"/> calls.
    /// </summary>
    public sealed class UpdateResults
    {
        private readonly ConcurrentBag<string> _updatedNugetsList = new ConcurrentBag<string>();


        /// <summary>
        /// True if the <see cref="IInsertionApi.UpdateVersions(string, string, string)"/> attempt completed, false otherwise.
        /// </summary>
        public bool Outcome => string.IsNullOrWhiteSpace(OutcomeDetails);

        /// <summary>
        /// Updated default.config NuGet package versions.
        /// </summary>
        public IEnumerable<string> UpdatedNuGets => _updatedNugetsList;

        public HashSet<string> IgnoreNuGets { get; set; }

        /// <summary>
        /// Duration in ms of <see cref="IInsertionApi.UpdateVersions(string, string, string)"/> attempt.
        /// </summary>
        public float DurationMilliseconds { get; set; }

        /// <summary>
        /// Describes the outcome of failed <see cref="IInsertionApi.UpdateVersions(string, string, string)"/> attempts.
        /// </summary>
        public string OutcomeDetails { get; set; }

        /// <summary>
        /// All the files that were modified during the update or
        /// the exceptions if there was an error while saving the file.
        /// </summary>
        public FileSaveResult[] FileSaveResults { get; set; }

        /// <summary>
        /// Adds the given package to the updated nuget list.
        /// </summary>
        public void AddPackage(string nugetId, string version)
        {
            _updatedNugetsList.Add($"{nugetId}, version: {version}");
        }

        public override string ToString()
        {
            return $"Validation {(Outcome ? "succeeded" : "failed")} with {UpdatedNuGets.Count()} matched assets ({DurationMilliseconds:N0}-ms)";
        }
    }
}