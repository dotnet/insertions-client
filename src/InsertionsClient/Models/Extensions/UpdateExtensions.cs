// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Net.Insertions.Models.Extensions
{
    internal static class UpdateExtensions
    {
        internal static string GetString(this Update update)
        {
            return update switch
            {
                Update.ExactMatch => "Update for case of matching manifest.json assets with multiple verions; where version of the exact matching NuGet asset was selected",
                Update.NoMatch => "No matching manifest.json assets for a given default.config NuGet",
                Update.Ignored => "Matching was skipped, because the package was explicitly ignored.",
                Update.NotAPackage => "Asset belongs to a resource or a supplementary file; not to a package.",
                _ => "Failed version update for default.config NuGets with matching manifest.json asset(s)"
            };
        }
    }
}