// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Net.Insertions.Api
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
        /// <param name="maxConcurrency">Optional: Level of concurrency.</param>
        /// <returns><see cref="IInsertionApi"/> An IInsertionApi instance.</returns>
        IInsertionApi Create(int? maxWaitSeconds = null, int? maxConcurrency = null);
    }
}