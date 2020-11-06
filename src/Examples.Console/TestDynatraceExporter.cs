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
using System.Diagnostics;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Exporter.Dynatrace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Trace;

namespace Examples.Console
{
    internal class TestDynatraceExporter
    {
        internal static async Task<int> RunAsync(string url, string apiToken, int pushIntervalInSecs, int totalDurationInMins)
        {
            var options = new DynatraceExporterOptions
            {
                Url = url,
                ApiToken = apiToken,
            };
            var dtExporter = new DynatraceMetricsExporter(options);

            // Create Processor (called Batcher in Metric spec, this is still not decided)
            var processor = new UngroupedBatcher();

            // Application which decides to enable OpenTelemetry metrics
            // would setup a MeterProvider and make it default.
            // All meters from this factory will be configured with the common processing pipeline.
            MeterProvider.SetDefault(Sdk.CreateMeterProviderBuilder()
                .SetProcessor(processor)
                .SetExporter(dtExporter)
                .SetPushInterval(TimeSpan.FromSeconds(pushIntervalInSecs))
                .Build());

            // The following shows how libraries would obtain a MeterProvider.
            // MeterProvider is the entry point, which provides Meter.
            // If user did not set the Default MeterProvider (shown in earlier lines),
            // all metric operations become no-ops.
            var meterProvider = MeterProvider.Default;
            var meter = meterProvider.GetMeter("MyMeter");

            // the rest is purely from Metrics API.
            var testCounter = meter.CreateInt64Counter("MyCounter");
            var testMeasure = meter.CreateInt64Measure("MyMeasure");
            var testObserver = meter.CreateInt64Observer("MyObservation", CallBackForMyObservation);
            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));
            var defaultContext = default(SpanContext);

            Stopwatch sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalMinutes < totalDurationInMins)
            {
                testCounter.Add(defaultContext, 100, meter.GetLabelSet(labels1));

                testMeasure.Record(defaultContext, 100, meter.GetLabelSet(labels1));
                testMeasure.Record(defaultContext, 500, meter.GetLabelSet(labels1));
                testMeasure.Record(defaultContext, 5, meter.GetLabelSet(labels1));
                testMeasure.Record(defaultContext, 750, meter.GetLabelSet(labels1));

                // Obviously there is no testObserver.Oberve() here, as Observer instruments
                // have callbacks that are called by the Meter automatically at each collection interval.

                await Task.Delay(1000);
                var remaining = (totalDurationInMins * 60) - sw.Elapsed.TotalSeconds;
                System.Console.WriteLine("Running and emitting metrics. Remaining time:" + (int)remaining + " seconds");
            }
            
            System.Console.WriteLine("Metrics server shutdown.");
            System.Console.WriteLine("Press Enter key to exit.");
            return 1;
        }

        internal static void CallBackForMyObservation(Int64ObserverMetric observerMetric)
        {
            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            observerMetric.Observe(Process.GetCurrentProcess().WorkingSet64, labels1);
        }
    }
}
