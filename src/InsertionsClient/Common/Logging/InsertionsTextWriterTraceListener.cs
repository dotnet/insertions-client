// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Net.Insertions.Common.Logging
{
    internal sealed class InsertionsTextWriterTraceListener : TextWriterTraceListener
    {
        private StringBuilder _stringBuilder = new StringBuilder(512);

        internal InsertionsTextWriterTraceListener(string fileName, string listenerName)
            : base(fileName, listenerName) 
        {

        }

        public override void WriteLine(string message)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(DateTime.Now.ToString("dd-M-yyyy hh:mm:ss.ffffff"));
            _stringBuilder.Append("|thread:").Append(Environment.CurrentManagedThreadId);
            _stringBuilder.Append("|").Append(message);

            base.WriteLine(_stringBuilder.ToString());
        }
    }
}