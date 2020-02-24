// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.Net.Insertions.Common.Logging
{
    internal sealed class MessageProcessor : ConcurrentQueue<MessageQueueItem>
    {
        private readonly int _delayMilliseconds = 150;

        private readonly double _maxThreadWaitSeconds = 1.5;


        internal MessageProcessor(Action<MessageQueueItem> action)
        {
            MessageAction = action ?? throw new ArgumentNullException(paramName: nameof(action));
            _ = Task.Factory.StartNew(Consume, TaskCreationOptions.LongRunning);
        }


        private Action<MessageQueueItem> MessageAction { get; }

        private void Consume()
        {
            while (true)
            {
                if (TryDequeue(out MessageQueueItem item))
                {
                    _ = Task.Factory.StartNew(() => MessageAction(item)).Wait(TimeSpan.FromSeconds(_maxThreadWaitSeconds));
                }
                else
                {
                    _ = Task.Delay(_delayMilliseconds).ConfigureAwait(false);
                }
            }
        }
    }
}