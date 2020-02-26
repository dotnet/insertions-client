// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Net.Insertions.Models
{
    internal enum Update
    {
        CommonVersion,
        ExactMatch,
        NoMatch,
        Ignored,
        MultipleConflictingMatches,
        VersionUnspecified,
    }
}