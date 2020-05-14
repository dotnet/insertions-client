// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.Serialization;

namespace Microsoft.Net.Insertions.Models
{
    /// <summary>
    /// Describes individual build assets.
    /// </summary>
    [DataContract(Name = "asset")]
    public sealed class Asset
    {
        /// <summary>
        /// NuGet name.
        /// </summary>
        [DataMember(Name = "name", IsRequired = true)]
        public string? Name { get; set; }

        /// <summary>
        /// NuGet name.
        /// </summary>
        [DataMember(Name = "version", IsRequired = true)]
        public string? Version { get; set; }


        public override string ToString()
        {
            return $"{Name ?? string.Empty} (v{Version})";
        }
    }
}