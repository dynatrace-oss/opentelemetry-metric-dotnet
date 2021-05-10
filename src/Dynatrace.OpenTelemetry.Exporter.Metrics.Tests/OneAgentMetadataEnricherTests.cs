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
using System.IO;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dynatrace.OpenTelemetry.Exporter.Metrics.Tests
{
    public class OneAgentMetadataEnricherTests
    {
        [Fact]
        public void ProcessGroupOkMultiline()
        {
            var enricher = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance);
            var metadata = enricher.ReadOneAgentMetadata(new string[] {
                "a=123",
                "b=456",
            });
            Assert.Collection(metadata,
              elem1 =>
              {
                  Assert.Equal("a", elem1.Key);
                  Assert.Equal("123", elem1.Value);
              },
              elem2 =>
              {
                  Assert.Equal("b", elem2.Key);
                  Assert.Equal("456", elem2.Value);
              });
        }

        [Fact]
        public void WrongSyntax()
        {
            var enricher = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance);
            Assert.Empty(enricher.ReadOneAgentMetadata(new string[] { "=0x5c14d9a68d569861" }));
            Assert.Empty(enricher.ReadOneAgentMetadata(new string[] { "otherKey=" }));
            Assert.Empty(enricher.ReadOneAgentMetadata(new string[] { "" }));
            Assert.Empty(enricher.ReadOneAgentMetadata(new string[] { "=" }));
            Assert.Empty(enricher.ReadOneAgentMetadata(new string[] { "===" }));
            Assert.Empty(enricher.ReadOneAgentMetadata(new string[] { }));
        }

        [Fact]
        public void IndirectionFileMissing()
        {
            var filesys = Mock.Of<IFileSystem>();
            Mock.Get(filesys).Setup(f => f.File.ReadAllText(It.IsAny<string>())).Throws<FileNotFoundException>();
            var targetList = new List<KeyValuePair<string, string>>();

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, filesys);

            unitUnderTest.EnrichWithDynatraceMetadata(targetList);
            Assert.Empty(targetList);
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionFileNotAccessible()
        {
            var filesys = Mock.Of<IFileSystem>();
            Mock.Get(filesys).Setup(f => f.File.ReadAllText(It.IsAny<string>())).Throws<UnauthorizedAccessException>();
            var targetList = new List<KeyValuePair<string, string>>();

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, filesys);

            unitUnderTest.EnrichWithDynatraceMetadata(targetList);
            Assert.Empty(targetList);
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionFileAnyOtherException()
        {
            var filesys = Mock.Of<IFileSystem>();
            // there is a whole host of exceptions that can be thrown by ReadAllText: https://docs.microsoft.com/en-us/dotnet/api/system.io.file.readalltext?view=net-5.0
            Mock.Get(filesys).Setup(f => f.File.ReadAllText(It.IsAny<string>())).Throws<Exception>();
            var targetList = new List<KeyValuePair<string, string>>();

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, filesys);

            unitUnderTest.EnrichWithDynatraceMetadata(targetList);
            Assert.Empty(targetList);
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionFileEmpty()
        {
            var filesys = Mock.Of<IFileSystem>();
            Mock.Get(filesys).Setup(f => f.File.ReadAllText(It.IsAny<string>())).Returns("");
            var targetList = new List<KeyValuePair<string, string>>();

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, filesys);

            unitUnderTest.EnrichWithDynatraceMetadata(targetList);
            Assert.Empty(targetList);
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            // if the OneAgent metadata file is empty, there should be no attempt at reading the contents.
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllLines(""), Times.Never());
        }

        [Fact]
        public void IndirectionFileContainsAdditionalText()
        {
            var filesys = Mock.Of<IFileSystem>();
            var indirectionFileContent =
            @"indirection_file_name.properties
            some other text
            and some more text";

            Mock.Get(filesys).Setup(f => f.File.ReadAllText(It.IsAny<string>())).Returns(indirectionFileContent);
            var targetList = new List<KeyValuePair<string, string>>();

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, filesys);

            unitUnderTest.EnrichWithDynatraceMetadata(targetList);
            Assert.Empty(targetList);
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            // if the OneAgent metadata file is empty, there should be no attempt at reading the contents.
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllLines(indirectionFileContent), Times.Once());
        }

        [Fact]
        public void IndirectionTargetMissing()
        {
            var filesys = Mock.Of<IFileSystem>();
            Mock.Get(filesys).Setup(f => f.File.ReadAllText(It.IsAny<string>())).Returns("indirection_file_name.properties");
            Mock.Get(filesys).Setup(f => f.File.ReadAllLines(It.IsAny<string>())).Throws<FileNotFoundException>();
            var targetList = new List<KeyValuePair<string, string>>();

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, filesys);

            unitUnderTest.EnrichWithDynatraceMetadata(targetList);
            Assert.Empty(targetList);
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllLines("indirection_file_name.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionTargetInvalidAccess()
        {
            var filesys = Mock.Of<IFileSystem>();
            Mock.Get(filesys).Setup(f => f.File.ReadAllText(It.IsAny<string>())).Returns("indirection_file_name.properties");
            Mock.Get(filesys).Setup(f => f.File.ReadAllLines(It.IsAny<string>())).Throws<AccessViolationException>();
            var targetList = new List<KeyValuePair<string, string>>();

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, filesys);

            unitUnderTest.EnrichWithDynatraceMetadata(targetList);
            Assert.Empty(targetList);
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllLines("indirection_file_name.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionTargetThrowsAnyOtherException()
        {
            var filesys = Mock.Of<IFileSystem>();
            Mock.Get(filesys).Setup(f => f.File.ReadAllText(It.IsAny<string>())).Returns("indirection_file_name.properties");
            Mock.Get(filesys).Setup(f => f.File.ReadAllLines(It.IsAny<string>())).Throws<Exception>();
            var targetList = new List<KeyValuePair<string, string>>();

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, filesys);

            unitUnderTest.EnrichWithDynatraceMetadata(targetList);
            Assert.Empty(targetList);
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllLines("indirection_file_name.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionTargetEmpty()
        {
            var filesys = Mock.Of<IFileSystem>();
            Mock.Get(filesys).Setup(f => f.File.ReadAllText(It.IsAny<string>())).Returns("indirection_file_name.properties");
            Mock.Get(filesys).Setup(f => f.File.ReadAllLines(It.IsAny<string>())).Returns(Array.Empty<string>());
            var targetList = new List<KeyValuePair<string, string>>();

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, filesys);

            unitUnderTest.EnrichWithDynatraceMetadata(targetList);
            Assert.Empty(targetList);
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllLines("indirection_file_name.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionTargetValid()
        {
            var filesys = Mock.Of<IFileSystem>();
            Mock.Get(filesys).Setup(f => f.File.ReadAllText(It.IsAny<string>())).Returns("indirection_file_name.properties");
            Mock.Get(filesys).Setup(f => f.File.ReadAllLines(It.IsAny<string>())).Returns(new string[] { "key1=value1", "key2=value2" });
            var targetList = new List<KeyValuePair<string, string>>();

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, filesys);

            unitUnderTest.EnrichWithDynatraceMetadata(targetList);
            Assert.NotEmpty(targetList);
            Assert.Equal(2, targetList.Count);
            Assert.Contains(KeyValuePair.Create("key1", "value1"), targetList);
            Assert.Contains(KeyValuePair.Create("key2", "value2"), targetList);
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllLines("indirection_file_name.properties"), Times.Once());
        }

        [Fact]
        public void IndirectionTargetValidWithInvalidLines()
        {
            var filesys = Mock.Of<IFileSystem>();
            Mock.Get(filesys).Setup(f => f.File.ReadAllText(It.IsAny<string>())).Returns("indirection_file_name.properties");
            Mock.Get(filesys).Setup(f => f.File.ReadAllLines(It.IsAny<string>())).Returns(new string[] { "key1=value1", "key2=", "=value2", "===" });
            var targetList = new List<KeyValuePair<string, string>>();

            var unitUnderTest = new OneAgentMetadataEnricher(NullLogger<DynatraceMetricsExporter>.Instance, filesys);

            unitUnderTest.EnrichWithDynatraceMetadata(targetList);
            Assert.NotEmpty(targetList);
            Assert.Equal(1, targetList.Count);
            Assert.Contains(KeyValuePair.Create("key1", "value1"), targetList);
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllText("dt_metadata_e617c525669e072eebe3d0f08212e8f2.properties"), Times.Once());
            Mock.Get(filesys).Verify(mock => mock.File.ReadAllLines("indirection_file_name.properties"), Times.Once());
        }
    }
}
