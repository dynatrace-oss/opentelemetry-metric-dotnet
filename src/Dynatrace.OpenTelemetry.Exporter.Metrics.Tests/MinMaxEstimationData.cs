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

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Tests
{
	public static class MinMaxEstimationData
	{
		public static IEnumerable<object[]> Data =>
			new List<object[]>
			{
				new object[]
				{
					// min: Values between the first two boundaries.
					new HistogramTestData
					{
						Boundaries = new[] { 1d, 2d, 3d, 4d, 5d },
						Count = 6,
						Values = new[] { 1.5, 3.2, 3.2, 3.5, 4.8, 4.9 },
						Sum = 21.1,
						Min = 1d,
						Max = 5d
					}
				},
				new object[]
				{
					// min: First bucket has value, use the first boundary as estimation instead of Inf.
					new HistogramTestData
					{
						Boundaries = new[] { 1d, 2d, 3d, 4d, 5d },
						Count = 8,
						Values = new[] { 0.5, 3.1, 3.1, 3.1, 6, 6, 6, 6.7 },
						Sum = 34.5,
						Min = 1d,
						Max = 5d
					}
				},
				new object[]
				{
					// min: Only the first bucket has values, use the mean (0.25) Otherwise, the min would be estimated as 1, and min <= avg would be violated.
					new HistogramTestData
					{
						Boundaries = new[] { 1d, 2d, 3d, 4d, 5d },
						Count = 3,
						Values = new[] { 0.3, 0.3, 0.15 },
						Sum = 0.75,
						Min = 0.25,
						Max = 1
					},
				},
				new object[]
				{
					// min: Just one bucket from -Inf to Inf, calculate the mean as min value.
					new HistogramTestData
					{
						Boundaries = Array.Empty<double>(),
						Count = 4,
						Values = new[] { 2, 2, 2, 2.8 },
						Sum = 8.8,
						Min = 2.2,
						Max = 2.2
					}
				},
				new object[]
				{
					// min: Just one bucket from -Inf to Inf, calculate the mean as min value.
					new HistogramTestData
					{
						Boundaries = Array.Empty<double>(),
						Count = 1,
						Values = new[] { 1.2 },
						Sum = 1.2,
						Min = 1.2,
						Max = 1.2
					},
				},
				new object[]
				{
					// min: Only the last bucket has a value, use the lower bound.
					new HistogramTestData
					{
						Boundaries = new[] { 1d, 2d, 3d, 4d, 5d },
						Count = 3,
						Values = new[] { 5.1, 5.1, 5.4 },
						Sum = 15.6,
						Min = 5,
						Max = 5.2
					}
				},
				new object[]
				{
					// max: Values between the last two boundaries.
					new HistogramTestData
					{
						Boundaries = new[] { 1d, 2d, 3d, 4d, 5d },
						Count = 6,
						Values = new[] { 1.5, 3.2, 3.2, 3.5, 4.8, 4.9 },
						Sum = 21.1,
						Min = 1d,
						Max = 5d
					}
				},
				new object[]
				{
					// max: Values between the last two boundaries.
					new HistogramTestData
					{
						Boundaries = new[] { 1d, 2d, 3d, 4d, 5d },
						Count = 8,
						Values = new[] { 0.5, 3.1, 3.1, 3.1, 6, 6, 6, 6.7 },
						Sum = 34.5,
						Min = 1,
						Max = 5
					}
				},
				new object[]
				{
					// max: Only the last bucket has values, use the mean (10.1) Otherwise, the max would be estimated as 5, and max >= avg would be violated.
					new HistogramTestData
					{
						Boundaries = new[] { 1d, 2d, 3d, 4d, 5d },
						Count = 2,
						Values = new[] { 5.2, 15 },
						Sum = 20.2,
						Min = 5,
						Max = 10.1
					}
				},
				new object[]
				{
					// max: Just one bucket from -Inf to Inf, calculate the mean as max value.
					new HistogramTestData
					{
						Boundaries = Array.Empty<double>(),
						Count = 4,
						Values = new[] { 2, 2, 2, 2.8 },
						Sum = 8.8,
						Min = 2.2,
						Max = 2.2
					}
				},
				new object[]
				{
					// max: Just one bucket from -Inf to Inf, calculate the mean as max value.
					new HistogramTestData
					{
						Boundaries = Array.Empty<double>(),
						Count = 1,
						Values = new[] { 1.2 },
						Sum = 1.2,
						Min = 1.2,
						Max = 1.2
					}
				},
				new object[]
				{
					// max: Max is larger than the sum, use the estimated boundary.
					new HistogramTestData
					{
						Boundaries = new[] { 0d, 5d },
						Count = 2,
						Values = new[] { 1, 1.3 },
						Sum = 2.3,
						Min = 0,
						Max = 5d
					}
				},
				new object[]
				{
					// max: Only the first bucket has a value, use the upper bound.
					new HistogramTestData
					{
						Boundaries = new[] { 1d, 2d, 3d, 4d, 5d },
						Count = 3,
						Values = new[] { 0.2, 0.4, 0.9 },
						Sum = 1.5,
						Min = 0.5,
						Max = 1
					}
				}
			};
	}
}
