// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.Serialization;

namespace Microsoft.DotNet.InsertionsClient.Models
{
    /// <summary>
    /// Represents a channel that a build from a manifest.file json should be pushed into.
    /// </summary>
    [DataContract(Name = "channel")]
    public sealed class Channel
    {
        [DataMember(Name = "id", IsRequired = true)]
        public int Id { get; set; }

        [DataMember(Name = "name", IsRequired = true)]
        public string? Name { get; set; }
    }
}
