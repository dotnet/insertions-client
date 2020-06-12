// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.DotNet.InsertionsClient.Api.Providers
{
    public sealed class InsertionApiFactory : IInsertionApiFactory
    {
        public IInsertionApi Create(TimeSpan? maxWaitSeconds = null, TimeSpan? maxDownloadSeconds = null, int ? maxConcurrency = null)
        {
            return new InsertionApi(maxWaitSeconds, maxDownloadSeconds, maxConcurrency);
        }
    }
}