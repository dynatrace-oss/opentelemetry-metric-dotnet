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

using System.Collections.Generic;

namespace OpenTelemetry.Exporter.Dynatrace
{
    /// <summary>
    /// Options to run dynatrace exporter.
    /// </summary>
    public class DynatraceExporterOptions
    {
        /// <summary>
        /// Gets or sets the dynatrace endpoint to send data to.
        ///
        /// For example:
        ///     Local OneAgent: http://127.0.0.1:14499/metrics/ingest (api-token NOT required)
        ///     Dynatrace Cluster: https://my-cluster/api/v2/metrics/ingest
        ///
        /// https://www.dynatrace.com/support/help/dynatrace-api/environment-api/metric-v2/post-ingest-metrics
        /// </summary>
        public string Url { get; set; } = "http://127.0.0.1:14499/metrics/ingest";

        /// <summary>
        /// Gets or sets the dynatrace api-token for authentication.
        ///
        /// How to aquire an api-token: https://www.dynatrace.com/support/help/dynatrace-api/basics/dynatrace-api-authentication/
        ///
        /// "metrics.ingest" permission is required for the api-token.
        /// </summary>
        public string ApiToken { get; set; }

        /// <summary>
        /// Gets automatically prefixed to a metric name.
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// Gets automatically added as metric labels/dimensions.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Tags { get; set; }
    }
}
