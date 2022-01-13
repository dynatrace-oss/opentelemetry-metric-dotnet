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
using OpenTelemetry.Metrics;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Tests.Utils
{
	/// <summary>
	/// Handy type to extract <see cref="MetricPoint"/>s from a <see cref="Metric"/>
	/// </summary>
	internal record MetricTest(DateTimeOffset EndTime)
	{
		internal long TimeStamp => EndTime.ToUnixTimeMilliseconds();

		internal static IEnumerable<MetricTest> FromMetricPoints(MetricPointsAccessor metricPoints)
		{
			foreach (var point in metricPoints)
			{
				yield return new MetricTest(point.EndTime);
			}
		}
	}
}
