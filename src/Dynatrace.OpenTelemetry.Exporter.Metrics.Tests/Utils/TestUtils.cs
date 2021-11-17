// <copyright file="Utils.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Tests
{
	internal class TestUtils
	{
		/// <summary>
		/// From: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/test/OpenTelemetry.Tests/Shared/Utils.cs
		/// Used to create meters for each test using the method name as the meter name.
		/// </summary>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static string GetCurrentMethodName()
		{
			var method = new StackFrame(1).GetMethod();
			return $"{method!.DeclaringType!.FullName}.{method.Name}";
		}
	}
}
