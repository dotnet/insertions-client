// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Net.Insertions.Api.Providers
{
    internal sealed class InsertionApiFactory : IInsertionApiFactory
    {
        public IInsertionApi Create(int? maxWaitSeconds = null, int? maxConcurrency = null)
        {
            return new InsertionApi(maxWaitSeconds, maxConcurrency);
        }
    }
}