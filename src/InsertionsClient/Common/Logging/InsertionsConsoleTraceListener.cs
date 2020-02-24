// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;

namespace Microsoft.Net.Insertions.Common.Logging
{
    public sealed class InsertionsConsoleTraceListener : ConsoleTraceListener
    {
        private readonly MessageProcessor _messageProcessor;


        internal InsertionsConsoleTraceListener()
            : base()
        {
            _messageProcessor = new MessageProcessor(x =>
            {
                base.WriteLine(x.ToString());
                base.Flush();
            });
        }

        public override void WriteLine(string message)
        {
            _messageProcessor.Enqueue(new MessageQueueItem(message, Environment.CurrentManagedThreadId));
        }
    }
}