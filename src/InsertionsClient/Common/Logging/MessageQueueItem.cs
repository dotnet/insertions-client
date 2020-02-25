// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.Net.Insertions.Common.Logging
{
    internal struct MessageQueueItem : IEquatable<MessageQueueItem>
    {
        public MessageQueueItem(string message, int threadId)
        {
            Message = message ?? string.Empty;
            TimeStamp = DateTime.UtcNow;
            ThreadId = threadId;
        }


        public string Message { get; }

        public int ThreadId { get; }

        public DateTime TimeStamp { get; }


        public bool Equals(MessageQueueItem other)
        {
            return ThreadId == other.ThreadId && TimeStamp == other.TimeStamp && Message == other.Message;
        }

        public override string ToString()
        {
            return $"{TimeStamp:dd-M-yyyy hh:mm:ss.ffffff}|thread:{ThreadId}|{Message}";
        }
    }
}