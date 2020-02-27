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
        /// <param name="maxWaitSeconds">Optional: boxed wait seconds integer.</param>
        /// <param name="maxConcurrency">Optional: boxed concurrency integer.</param>
        /// <returns><see cref="IInsertionApi"/> instance.</returns>
        IInsertionApi Create(int? maxWaitSeconds = null, int? maxConcurrency = null);
    }
}