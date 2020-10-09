// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.DotNet.InsertionsClient.Api.Providers
{
    // IInsertionApiFactory is not use anywhere. Also, an interface to a factory is many layer of dependency injection, is
    // that necessary?
    
    // InsertionApiFactory is not used in dependency injection. It should just be the constructor of InsertionApi.
    // There is no use for IInsertionApi too.
    public sealed class InsertionApiFactory : IInsertionApiFactory
    {
        public IInsertionApi Create(TimeSpan? maxWaitSeconds = null, TimeSpan? maxDownloadSeconds = null, int ? maxConcurrency = null)
        {
            return new InsertionApi(maxWaitSeconds, maxDownloadSeconds, maxConcurrency);
        }
    }
}