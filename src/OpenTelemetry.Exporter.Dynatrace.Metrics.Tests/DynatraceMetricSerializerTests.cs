using System;
using System.Collections.Generic;
using System.Linq;
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Dynatrace.Metrics.Tests
{
    public class DynatraceMetricSerializerTests
    {
        [Fact]
        public void SerializeLongSum()
        {
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628881).UtcDateTime;

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));
            labels1.Add(new KeyValuePair<string, string>("dim2", "value2"));
            var m1 = new Metric("namespace1", "metric1", "Description", AggregationType.LongSum);
            m1.Data.Add(new Int64SumData
            {
                Labels = labels1,
                Sum = 100,
                Timestamp = timestamp
            });

            string serialized = new DynatraceMetricSerializer().SerializeMetric(m1);
            string expected = "namespace1.metric1,dim1=value1,dim2=value2 count,delta=100 1604660628881";
            Assert.Equal(expected, serialized);
        }
    }
}
