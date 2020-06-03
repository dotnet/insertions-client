// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.DotNet.InsertionsClient.Api
{
    /// <summary>
    /// Factory of <see cref="IInsertionApi"/> instances.
    /// </summary>
    public interface IInsertionApiFactory
    {
        /// <summary>
        /// Creates an <see cref="IInsertionApi"/> instances.
        /// </summary>
        /// <param name="maxWaitSeconds">Optional: Maximum number of seconds that the application
        /// is allowed to run. After that, operation will be cancelled.</param>
        /// <param name="maxDownloadSeconds">Optional: Maximum number of seconds that the application
        /// is allowed to spend on downloading and processing nuget packages.</param>
        /// <param name="maxConcurrency">Optional: Level of concurrency.</param>
        /// <returns><see cref="IInsertionApi"/> An IInsertionApi instance.</returns>
        IInsertionApi Create(TimeSpan? maxWaitSeconds = null, TimeSpan? maxDownloadSeconds = null, int? maxConcurrency = null);
    }
}