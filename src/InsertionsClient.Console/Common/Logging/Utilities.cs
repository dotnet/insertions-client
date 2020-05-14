// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text;

namespace Microsoft.Net.Insertions.Common.Logging
{
    static class Utilities
    {
        private static StringBuilder _logBuilder = new StringBuilder(512);

        /// <remarks> This method is not thread safe! </remarks>
        internal static string MessageToLogString(string message)
        {
            _ = _logBuilder.Clear();
            _ = _logBuilder.Append(DateTime.Now.ToString("dd-M-yyyy hh:mm:ss.ffffff"));
            _ = _logBuilder.Append("|thread:").Append(Environment.CurrentManagedThreadId);
            _ = _logBuilder.Append("|").Append(message);

            return _logBuilder.ToString();
        }
    }
}
