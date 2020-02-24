// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;

namespace Microsoft.Net.Insertions.Common.Logging
{
    internal sealed class InsertionsTextWriterTraceListener : TextWriterTraceListener
    {
        private readonly MessageProcessor _messageProcessor;


        internal InsertionsTextWriterTraceListener(string fileName, string listenerName)
            : base(fileName, listenerName)
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