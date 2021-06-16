
// <copyright company="Dynatrace LLC">
// Copyright 2021 Dynatrace LLC
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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using OpenTelemetry.Metrics.Export;
using Xunit;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Tests
{
    public class DynatraceMetricsExporterTests
    {
        private static readonly ILogger<DynatraceMetricsExporter> _logger = NullLogger<DynatraceMetricsExporter>.Instance;

        [Fact]
        public async void TestDefaultOptions()
        {
            var mockMessageHandler = new Mock<HttpMessageHandler>();
            mockMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("test")
            });

            var exporter = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));

            var result = await exporter.ExportAsync(new List<Metric> { CreateMetric() }, CancellationToken.None);

            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post), ItExpr.IsAny<CancellationToken>());
            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.AbsoluteUri == "http://localhost:14499/metrics/ingest"), ItExpr.IsAny<CancellationToken>());
            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => !req.Headers.Contains("Api-Token")), ItExpr.IsAny<CancellationToken>());
            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Headers.Contains("User-Agent")), ItExpr.IsAny<CancellationToken>());
            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Headers.GetValues("User-Agent").Count() == 1), ItExpr.IsAny<CancellationToken>());
            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Headers.GetValues("User-Agent").First() == "opentelemetry-metric-dotnet"), ItExpr.IsAny<CancellationToken>());
            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Content.ReadAsStringAsync().Result == "namespace1.metric1,label1=value1,label2=value2,dt.metrics.source=opentelemetry count,delta=100 1604660628881" + Environment.NewLine), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async void TestUriAndToken()
        {
            var mockMessageHandler = new Mock<HttpMessageHandler>();
            mockMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("test")
            });

            var exporter = new DynatraceMetricsExporter(new DynatraceExporterOptions { Url = "http://my.url", ApiToken = "test-token" }, null, new HttpClient(mockMessageHandler.Object));

            var result = await exporter.ExportAsync(new List<Metric> { CreateMetric() }, CancellationToken.None);

            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.AbsoluteUri == "http://my.url/"), ItExpr.IsAny<CancellationToken>());
            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Headers.Contains("Authorization")), ItExpr.IsAny<CancellationToken>());
            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Headers.GetValues("Authorization").Count() == 1), ItExpr.IsAny<CancellationToken>());
            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Headers.GetValues("Authorization").First() == "Api-Token test-token"), ItExpr.IsAny<CancellationToken>());
            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Content.ReadAsStringAsync().Result == "namespace1.metric1,label1=value1,label2=value2,dt.metrics.source=opentelemetry count,delta=100 1604660628881" + Environment.NewLine), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async void TestSendInBatches()
        {
            var mockMessageHandler = new Mock<HttpMessageHandler>();
            mockMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("test")
            });

            var exporter = new DynatraceMetricsExporter(null, null, new HttpClient(mockMessageHandler.Object));
            var metrics = new List<Metric>();
            for (int i = 0; i < 1001; i++)
            {
                metrics.Add(CreateMetric());
            }

            var result = await exporter.ExportAsync(metrics, CancellationToken.None);

            // for more than 1000 lines, SendAsync is called twice.
            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(2), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async void TestExportMultipleWithPrefix()
        {
            var mockMessageHandler = new Mock<HttpMessageHandler>();
            mockMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("test")
            });

            var exporter = new DynatraceMetricsExporter(new DynatraceExporterOptions { Prefix = "my.prefix" }, null, new HttpClient(mockMessageHandler.Object));

            var result = await exporter.ExportAsync(CreateMetrics(), CancellationToken.None);
            var expectedString = "my.prefix.namespace1.metric1,dt.metrics.source=opentelemetry count,delta=100 1604660628881" + Environment.NewLine + "my.prefix.namespace2.metric2,dt.metrics.source=opentelemetry count,delta=200 1604660628881" + Environment.NewLine;

            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post), ItExpr.IsAny<CancellationToken>());
            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.AbsoluteUri == "http://localhost:14499/metrics/ingest"), ItExpr.IsAny<CancellationToken>());
            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => !req.Headers.Contains("Api-Token")), ItExpr.IsAny<CancellationToken>());
            mockMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Content.ReadAsStringAsync().Result == expectedString), ItExpr.IsAny<CancellationToken>());
        }

        private List<Metric> CreateMetrics()
        {
            var metrics = new List<Metric>();

            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628881).UtcDateTime;

            var metric1 = new Metric("namespace1", "metric1", "Description", AggregationType.LongSum);
            metric1.Data.Add(new Int64SumData
            {
                Sum = 100,
                Timestamp = timestamp
            });

            metrics.Add(metric1);

            var metric2 = new Metric("namespace2", "metric2", "Description", AggregationType.LongSum);
            metric2.Data.Add(new Int64SumData
            {
                Sum = 200,
                Timestamp = timestamp
            });
            metrics.Add(metric2);

            return metrics;

        }

        private Metric CreateMetric()
        {
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1604660628881).UtcDateTime;

            var labels = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("label1", "value1"),
                new KeyValuePair<string, string>("label2", "value2")
            };
            var metric = new Metric("namespace1", "metric1", "Description", AggregationType.LongSum);
            metric.Data.Add(new Int64SumData
            {
                Labels = labels,
                Sum = 100,
                Timestamp = timestamp
            });

            return metric;
        }

    }
}
