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

using System.IO;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics
{
    /// <summary>
    /// Provides specific file reading operations, which are passed through to the
    /// respective methods on <c>System.IO.File</c>
    /// </summary>
    internal interface IFileReader
    {
        string ReadAllText(string filename);
        string[] ReadAllLines(string filename);
    }

    internal class DefaultFileReader : IFileReader
    {
        /// <summary>Returns the result of File.ReadAllLines(filename).</summary>
        public string[] ReadAllLines(string filename)
        {
            return File.ReadAllLines(filename);
        }

        /// <summary>Returns the result of File.ReadAllText(filename).</summary>
        public string ReadAllText(string filename)
        {
            return File.ReadAllText(filename);
        }
    }
}
