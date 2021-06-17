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

using CommandLine;
using System.Threading.Tasks;

namespace Examples.Console
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<DynatraceOptions>(args)
                .MapResult(
                    (DynatraceOptions options) => ExampleDynatraceExporter.RunAsync(options.Url, options.ApiToken, options.PushIntervalInSecs, options.DurationInMins, !options.DisableOneAgentMetadataEnrichment),
                    errs => Task.FromResult(0));

        }
    }

    [Verb("dynatrace", HelpText = "Specify the options required to test Dynatrace")]
    internal class DynatraceOptions
    {
        [Option('i', "pushIntervalInSecs", Default = 15, HelpText = "The interval at which metrics are pushed to Dynatrace.", Required = false)]
        public int PushIntervalInSecs { get; set; }

        [Option('d', "duration", Default = 2, HelpText = "Total duration in minutes to run the demo. Run at least for one minute to see metrics flowing.", Required = false)]
        public int DurationInMins { get; set; }

        [Option('u', "url", HelpText = "Dynatrace metrics ingest API URL, including the '/api/v2/metrics/ingest' suffix.", Required = false)]
        public string Url { get; set; }

        [Option('a', "apiToken",  HelpText = "Dynatrace API authentication token with the 'metrics.ingest' permission.", Required = false)]
        public string ApiToken { get; set; }

        [Option('n', "noOneAgentEnrichment",  HelpText = "Disable automatic label enrichment via OneAgent metadata.", Required = false)]
        public bool DisableOneAgentMetadataEnrichment { get; set; } = false;
    }
}
