// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Net.Insertions.Models.Extensions
{
    internal static class UpdateExtensions
    {
        internal static string GetString(this Update update)
        {
            return update switch
            {
                Update.CommonVersion => "Update based on the common version of matching manifest.json asset(s)",
                Update.ExactMatch => "Update for case of matching manifest.json assets with multiple verions; where version of the exact matching NuGet asset was selected",
                Update.NoMatch => "No matching manifest.json assets for a given default.config NuGet",
                Update.Ignored => "Matching was skipped, because the package was explicitly ignored.",
                _ => "Failed version update for default.config NuGets with matching manifest.json asset(s)"
            };
        }
    }
}