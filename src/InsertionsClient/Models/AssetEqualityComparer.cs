// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

namespace Microsoft.Net.Insertions.Models
{
    internal sealed class AssetEqualityComparer : IEqualityComparer<Asset>
    {
        public bool Equals(Asset? x, Asset? y)
        {
            if (x == null)
            {
                return y == null;
            }
            if (y == null)
            {
                return false;
            }
            return x.Name == y.Name && x.Version == y.Version;
        }

        public int GetHashCode(Asset obj)
        {
            return obj == null ? -1 : (obj.Name?.GetHashCode() ?? -1) ^ (obj.Version?.GetHashCode() ?? -1);
        }
    }
}