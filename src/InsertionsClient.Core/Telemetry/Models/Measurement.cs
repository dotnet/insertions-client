// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Models;
using System;

namespace Microsoft.DotNet.InsertionsClient.Telemetry.Models
{
    internal sealed class Measurement
    {
        public Measurement(Update outcome, long latencyInTicks)
        {
            Update = outcome;
            LatencyMs = latencyInTicks / 10000d;
            TimeStamp = DateTime.UtcNow;
        }

        public Update Update { get; }

        public double LatencyMs { get; }

        public DateTime TimeStamp { get; }

        public override string ToString()
        {
            return $"{Update} {LatencyMs:N0}-ms";
        }
    }
}