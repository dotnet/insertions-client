// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Models;
using System;

namespace Microsoft.Net.Insertions.Telemetry.Models
{
    internal sealed class Measurement
    {
        public Measurement(Update outcome, double latency)
        {
            Update = outcome;
            LatencyMs = latency;
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