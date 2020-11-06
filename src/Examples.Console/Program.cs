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
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Examples.Console
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<DynatraceOptions>(args)
                .MapResult(
                    (DynatraceOptions options) => TestDynatraceExporter.RunAsync(options.Url, options.ApiToken, options.PushIntervalInSecs, options.DurationInMins),
                    errs => Task.FromResult(0));

            System.Console.ReadLine();
        }
    }

    [Verb("dynatrace", HelpText = "Specify the options required to test Dynatrace")]
    internal class DynatraceOptions
    {
        [Option('i', "pushIntervalInSecs", Default = 15, HelpText = "The interval at which Push controller pushes metrics.", Required = false)]
        public int PushIntervalInSecs { get; set; }

        [Option('d', "duration", Default = 2, HelpText = "Total duration in minutes to run the demo. Run atleast for a min to see metrics flowing.", Required = false)]
        public int DurationInMins { get; set; }

        [Option('u', "url", Default = "http://127.0.0.1:14499/metrics/ingest", HelpText = "Dynatrace metrics ingest API URL.", Required = false)]
        public string Url { get; set; }

        [Option('a', "apiToken", Default = "", HelpText = "Dynatrace API authentication token.", Required = false)]
        public string ApiToken { get; set; }
    }

}
