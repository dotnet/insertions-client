// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.Net.Insertions.Models
{
    /// <summary>
    /// Defines a manifest.json build.
    /// </summary>
    [DataContract(Name = "build")]
    public sealed class Build
    {
        /// <summary>
        /// Repository URL for which member <see cref="Asset"/> items are relevant.
        /// </summary>
        [DataMember(Name = "repo", IsRequired = true)]
        public string Repository { get; set; }

        /// <summary>
        /// Relevant Commit id.
        /// </summary>
        [DataMember(Name = "commit", IsRequired = true)]
        public string Commit { get; set; }

        /// <summary>
        /// Build GIT branch.
        /// </summary>
        [DataMember(Name = "branch", IsRequired = true)]
        public string Branch { get; set; }

        /// <summary>
        /// Boxed timestamp for build creation.
        /// </summary>
        [DataMember(Name = "produced", IsRequired = true)]
        public string Produced { get; set; }

        /// <summary>
        /// Build number.
        /// </summary>
        [DataMember(Name = "buildNumber", IsRequired = true)]
        public string BuildNumber { get; set; }

        /// <summary>
        /// Build ID.
        /// </summary>
        [DataMember(Name = "barBuildId", IsRequired = true)]
        public int BarBuildId { get; set; }

        /// <summary>
        /// Collection of build assets (i.e. NuGets)
        /// </summary>
        [DataMember(Name = "assets", IsRequired = true)]
        public List<Asset> Assets { get; set; }


        public override string ToString()
        {
            return $"Build from repo: {Repository ?? string.Empty}";
        }
    }
}