// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;

namespace Microsoft.Net.Insertions.Common.Logging
{
    internal sealed class InsertionsTextWriterTraceListener : TextWriterTraceListener
    {
        internal InsertionsTextWriterTraceListener(string fileName, string listenerName)
            : base(fileName, listenerName)
        {

        }

        public override void WriteLine(string message)
        {
            base.WriteLine(Utilities.MessageToLogString(message));
            Flush();
        }
    }
}