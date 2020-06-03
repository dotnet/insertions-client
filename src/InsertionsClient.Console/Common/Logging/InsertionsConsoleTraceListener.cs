// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;

namespace Microsoft.DotNet.InsertionsClient.Common.Logging
{
    internal sealed class InsertionsConsoleTraceListener : ConsoleTraceListener
    {
        public override void WriteLine(string message)
        {
            base.WriteLine(Utilities.MessageToLogString(message));
            Flush();
        }
    }
}