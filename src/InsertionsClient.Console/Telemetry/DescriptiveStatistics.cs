// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Telemetry.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Net.Insertions.Telemetry
{
    internal sealed class DescriptiveStatistics
    {
        private readonly IEnumerable<Measurement> _data;


        public DescriptiveStatistics(IEnumerable<Measurement> data)
        {
            _data = data ?? Enumerable.Empty<Measurement>();
        }


        public double Minimum =>
            _data.Any() ? _data.Min(x => x.LatencyMs) : 0;

        public double Maximum =>
            _data.Any() ? _data.Max(x => x.LatencyMs) : 0;

        public double Average =>
            _data.Any() ? _data.Average(x => x.LatencyMs) : 0;

        public int N => _data.Count();


        public override string ToString()
        {
            StringBuilder txt = new StringBuilder();
            _ = txt.AppendLine($"No. Items: {N:n0}");
            _ = txt.AppendLine($"Average: {Average:N4}-ms");
            _ = txt.AppendLine($"Minimum: {Minimum:N4}-ms");
            _ = txt.AppendLine($"Maximum: {Maximum:N4}-ms");
            return txt.ToString();
        }
    }
}