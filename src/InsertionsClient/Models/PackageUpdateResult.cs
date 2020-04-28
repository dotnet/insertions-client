// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.Net.Insertions.Models
{
    /// <summary>
    /// Stores the result of a package version update operation
    /// </summary>
    public struct PackageUpdateResult : IEquatable<PackageUpdateResult>
    {
        /// <summary>
        /// Id of the package that was updated.
        /// </summary>
        public string PackageId { get; private set; }

        /// <summary>
        /// Version of the package before the change.
        /// </summary>
        public string PreviousVersion { get; private set; }

        /// <summary>
        /// Version of the package after the change.
        /// </summary>
        public string NewVersion { get; private set; }

        /// <summary>
        /// Creates an instance of PackageUpdateResult.
        /// </summary>
        /// <param name="packageId">Id of the package that was updated.</param>
        /// <param name="previousVersion">Version of the package before the change.</param>
        /// <param name="newVersion">Version of the package after the change.</param>
        internal PackageUpdateResult(string packageId, string previousVersion, string newVersion)
        {
            PackageId = packageId;
            PreviousVersion = previousVersion;
            NewVersion = newVersion;
        }

        /// <summary>
        /// Compares given instance to this.
        /// </summary>
        /// <param name="other">Instance to compare to this.</param>
        /// <returns>True if both instances have the same data. False otherwise.</returns>
        public bool Equals(PackageUpdateResult other)
        {
            return PackageId == other.PackageId &&
                PreviousVersion == other.PreviousVersion &&
                NewVersion == other.NewVersion;
        }
    }
}
