// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Models;
using Microsoft.DotNet.InsertionsClient.Telemetry.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.InsertionsClient.Telemetry
{
    internal sealed class MeasurementsSession
    {
        private readonly ConcurrentBag<Measurement> _dataPoints = new ConcurrentBag<Measurement>();


        public DescriptiveStatistics this[Update item]
        {
            get
            {
                IEnumerable<Measurement> items = _dataPoints.Where(x => x.Update == item);
                return new DescriptiveStatistics(items == null || !items.Any() ? Enumerable.Empty<Measurement>() : items);
            }
        }

        public void AddMeasurement(Update update, long latencyInTicks)
        {
            AddMeasurement(new Measurement(update, latencyInTicks));
        }

        public void AddMeasurement(Measurement measurement)
        {
            _dataPoints.Add(measurement);
        }
    }
}