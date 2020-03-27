// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

namespace Microsoft.Net.Insertions.Common.Constants
{
    internal static class InsertionConstants
    {
        internal const string DefaultConfigFile = "default.config";

        internal const string ManifestFile = "manifest.json";

        internal static readonly HashSet<string> DefaultDevUxTeamPackages = new HashSet<string>
        {
            "Microsoft.VisualStudio.LiveShare",
            "System.Reflection.Metadata",
            "VS.ExternalAPIs.MSBuild",
            "VS.Tools.Net.Core.SDK.Resolver",
            "VS.Tools.Net.Core.SDK.x86"
        };
    }
}