// <copyright company="Dynatrace LLC">
// Copyright 2020 Dynatrace LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using OpenTelemetry.Metrics.Export;
using Xunit;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Tests
{
    public class DynatraceMetricSerializerTests
    {
        [Fact]
        public void SerializeLongSum()
        {
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628881).UtcDateTime;

            var labels1 = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("dim1", "value1"),
                new KeyValuePair<string, string>("dim2", "value2")
            };
            var m1 = new Metric("namespace1", "metric1", "Description", AggregationType.LongSum);
            m1.Data.Add(new Int64SumData
            {
                Labels = labels1,
                Sum = 100,
                Timestamp = timestamp
            });

            string serialized = new DynatraceMetricSerializer().SerializeMetric(m1);
            string expected = "namespace1.metric1,dim1=value1,dim2=value2 count,delta=100 1604660628881" + Environment.NewLine;
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeWithoutLabels()
        {
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628881).UtcDateTime;

            var m1 = new Metric("namespace1", "metric1", "Description", AggregationType.LongSum);
            m1.Data.Add(new Int64SumData
            {
                Labels = new List<KeyValuePair<string, string>>(),
                Sum = 100,
                Timestamp = timestamp
            });

            string serialized = new DynatraceMetricSerializer().SerializeMetric(m1);
            string expected = "namespace1.metric1 count,delta=100 1604660628881" + Environment.NewLine;
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void PrefixOption()
        {
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628881).UtcDateTime;

            var labels1 = new List<KeyValuePair<string, string>>();
            var m1 = new Metric("namespace1", "metric1", "Description", AggregationType.LongSum);
            m1.Data.Add(new Int64SumData
            {
                Labels = labels1,
                Sum = 100,
                Timestamp = timestamp
            });

            string serialized = new DynatraceMetricSerializer(prefix: "prefix1").SerializeMetric(m1);
            string expected = "prefix1.namespace1.metric1 count,delta=100 1604660628881" + Environment.NewLine;
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void DimensionsOption()
        {
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628881).UtcDateTime;

            var labels1 = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("dim1", "value1"),
                new KeyValuePair<string, string>("dim2", "value2")
            };
            var m1 = new Metric("namespace1", "metric1", "Description", AggregationType.LongSum);
            m1.Data.Add(new Int64SumData
            {
                Labels = labels1,
                Sum = 100,
                Timestamp = timestamp
            });

            string serialized = new DynatraceMetricSerializer(dimensions: new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("dimension1", "value1") ,
                new KeyValuePair<string, string>("dimension2", "value2") ,
                new KeyValuePair<string, string>("dimension3", "value3") ,
            }).SerializeMetric(m1);
            string expected = "namespace1.metric1,dim1=value1,dim2=value2,dimension1=value1,dimension2=value2,dimension3=value3 count,delta=100 1604660628881" + Environment.NewLine;
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeLongSumBatch()
        {
            var labels1 = new List<KeyValuePair<string, string>>{
                new KeyValuePair<string, string>("dim1", "value1"),
                new KeyValuePair<string, string>("dim2", "value2")
            };
            var m1 = new Metric("namespace1", "metric1", "Description", AggregationType.LongSum);
            m1.Data.Add(new Int64SumData
            {
                Labels = labels1,
                Sum = 100,
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628881).UtcDateTime
            });
            m1.Data.Add(new Int64SumData
            {
                Labels = labels1,
                Sum = 130,
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628882).UtcDateTime
            });
            m1.Data.Add(new Int64SumData
            {
                Labels = labels1,
                Sum = 150,
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628883).UtcDateTime
            });

            string serialized = new DynatraceMetricSerializer().SerializeMetric(m1);
            string expected =
                "namespace1.metric1,dim1=value1,dim2=value2 count,delta=100 1604660628881" + Environment.NewLine +
                "namespace1.metric1,dim1=value1,dim2=value2 count,delta=130 1604660628882" + Environment.NewLine +
                "namespace1.metric1,dim1=value1,dim2=value2 count,delta=150 1604660628883" + Environment.NewLine;
            Assert.Equal(expected, serialized);
        }
    }
}
