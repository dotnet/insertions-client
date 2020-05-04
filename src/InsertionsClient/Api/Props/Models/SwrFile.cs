// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

namespace Microsoft.Net.Insertions.Api.Props.Models
{
    /// <summary>
    /// Represents an SWR file in a VS repo local working directory.
    /// </summary>
    internal sealed class SwrFile
    {
        /// <summary>
        /// Full path to the swr file.
        /// </summary>
        public readonly string Path;

        /// <summary>
        /// Paths to payloads mentioned in the file.
        /// </summary>
        public readonly List<PayloadPath> PayloadPaths = new List<PayloadPath>();

        /// <summary>
        /// Creates an instance of <see cref="SwrFile"/>.
        /// Doesn't access the file, only stores the path.
        /// </summary>
        /// <param name="path">Path to the file on disk</param>
        public SwrFile(string path)
        {
            Path = path;
        }
    }
}
