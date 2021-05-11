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
        public void TagsOption()
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

            string serialized = new DynatraceMetricSerializer(tags: new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("tag1", "value1") ,
                new KeyValuePair<string, string>("tag2", "value2") ,
                new KeyValuePair<string, string>("tag3", "value3") ,
            }).SerializeMetric(m1);
            string expected = "namespace1.metric1,dim1=value1,dim2=value2,tag1=value1,tag2=value2,tag3=value3 count,delta=100 1604660628881" + Environment.NewLine;
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

        [Theory]
        [InlineData("just.a.normal.key", "just.a.normal.key")]
        [InlineData("~0something", "something")]
        [InlineData("some~thing", "some_thing")]
        [InlineData("some~ä#thing", "some_thing")]
        [InlineData("a..b", "a.b")]
        [InlineData("a.....b", "a.b")]
        [InlineData("asd", "asd")]
        [InlineData(".", "")]
        [InlineData(".a", "a")]
        [InlineData("a.", "a")]
        [InlineData(".a.", "a")]
        [InlineData("_a", "a")]
        [InlineData("a_", "a_")]
        [InlineData("_a_", "a_")]
        [InlineData(".a_", "a_")]
        [InlineData("_a.", "a")]
        [InlineData("._._a_._._", "a_")]
        [InlineData("ä_äa", "__a")]
        [InlineData("test..empty.test", "test.empty.test")]
        [InlineData("a,,,b  c=d\\e\\ =,f", "a_b_c_d_e_f")]
        [InlineData("a!b\"c#d$e%f&g'h(i)j*k+l,m-n.o/p:q;r<s=t>u?v@w[x]y\\z^0 1_2;3{4|5}6~7", "a_b_c_d_e_f_g_h_i_j_k_l_m-n.o_p:q_r_s_t_u_v_w_x_y_z_0_1_2_3_4_5_6_7")]
        public void MetricAndDimensionKeyNormalizer(string input, string expected)
        {
            Assert.Equal(expected, DynatraceMetricSerializer.ToMintMetricKey(input));
            Assert.Equal(expected, DynatraceMetricSerializer.ToMintDimensionKey(input));
        }

        [Theory]
        [InlineData("Preserve.thE_cAsing", "Preserve.thE_cAsing")]
        public void MetricKeyNormalizer(string input, string expected)
        {
            Assert.Equal(expected, DynatraceMetricSerializer.ToMintMetricKey(input));
        }

        [Theory]
        [InlineData("to.LOWER_CaSe", "to.lower_case")]
        public void DimensionKeyNormalizer(string input, string expected)
        {
            Assert.Equal(expected, DynatraceMetricSerializer.ToMintDimensionKey(input));
        }

        [Theory]
        [InlineData("it-preserves_Capital.lEtters", "it-preserves_Capital.lEtters")]
        [InlineData(" spaces escaped ", "\\ spaces\\ escaped\\ ")]
        [InlineData("wait,a,minute", "wait\\,a\\,minute")]
        [InlineData("equality=fine", "equality\\=fine")]
        [InlineData("a,,,b c=d\\e", "a\\,\\,\\,b\\ c\\=d\\\\e")]
        [InlineData("a!b\"c#d$e%f&g'h(i)j*k+l,m-n.o/p:q;r<s=t>u?v@w[x]y\\z^0 1_2;3{4|5}6~7", "a_b_c_d_e_f_g_h_i_j_k_l\\,m-n.o_p:q_r_s\\=t_u_v_w_x_y\\\\z_0\\ 1_2_3_4_5_6_7")]
        public void DimensionValueNormalizer(string input, string expected)
        {
            Assert.Equal(expected, DynatraceMetricSerializer.ToMintDimensionValue(input));
        }
    }
}
