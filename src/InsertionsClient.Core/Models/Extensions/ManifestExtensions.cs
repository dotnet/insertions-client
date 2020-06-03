// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.DotNet.InsertionsClient.Models.Extensions
{
    internal static class ManifestExtensions
    {
        internal static bool Validate(this Manifest manifest)
        {
            return manifest != null && manifest.Builds != null && manifest.Builds.Count > 0;
        }
    }
}