// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.DotNet.InsertionsClient.Models
{
    /// <summary>
    /// Model for manifest.json Builds collection.
    /// </summary>
    [DataContract(Name = "manifest")]
    public sealed class Manifest
    {
        /// <summary>
        /// Collection of <see cref="Build"/> instances detailed in manifest.json.
        /// </summary>
        [DataMember(Name = "builds", IsRequired = true)]
        public List<Build>? Builds { get; set; }


        public override string ToString()
        {
            return $"Manifest.  # of builds: {Builds?.Count ?? 0}";
        }
    }
}
