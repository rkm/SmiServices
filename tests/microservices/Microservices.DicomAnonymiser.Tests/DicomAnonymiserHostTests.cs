using Microservices.DicomAnonymiser.Anonymisers;
using Moq;
using NUnit.Framework;
using Smi.Common.Messages;
using Smi.Common.Messages.Extraction;
using Smi.Common.Options;
using Smi.Common.Tests;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading;

namespace Microservices.DicomAnonymiser.Tests
{
    [RequiresRabbit]
    public class DicomAnonymiserHostTests
    {
        #region Fixture Methods

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            TestLogger.Setup();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() { }


        #endregion

        #region Test Methods

        [SetUp]
        public void SetUp() { }

        [TearDown]
        public void TearDown() { }

        #endregion

        #region Tests

        [Test]
        public void Integration_HappyPath_MockAnonymiser()
        {
            GlobalOptions globals = new GlobalOptionsFactory().Load(nameof(Integration_HappyPath_MockAnonymiser));

            var mockFs = new MockFileSystem();

            var dicomRoot = mockFs.Directory.CreateDirectory("dicom");
            globals.FileSystemOptions.FileSystemRoot = dicomRoot.FullName;

            var extractRoot = mockFs.Directory.CreateDirectory("extractRoot");
            globals.FileSystemOptions.ExtractRoot = extractRoot.FullName;

            var fakeDicom = mockFs.Path.Combine(dicomRoot.FullName, "foo.dcm");
            mockFs.File.Create(fakeDicom);

            var extractDir = mockFs.Directory.CreateDirectory(mockFs.Path.Combine(extractRoot.FullName, "extractDir"));

            var testExtractFileMessage = new ExtractFileMessage
            {
                ExtractionJobIdentifier = Guid.NewGuid(),
                ProjectNumber = "1234",
                ExtractionDirectory = extractDir.Name,
                JobSubmittedAt = DateTime.UtcNow,
                IsIdentifiableExtraction = false,
                IsNoFilterExtraction = false,

                DicomFilePath = "foo.dcm",
                OutputPath = "foo-an.dcm",
            };

            var mockAnonymiser = new Mock<IDicomAnonymiser>(MockBehavior.Strict);
            mockAnonymiser
                .Setup(
                    x => x.Anonymise(
                        It.Is<IFileInfo>(x => x.FullName == fakeDicom),
                        It.Is<IFileInfo>(x => x.FullName == mockFs.Path.Combine(extractDir.FullName, "foo-an.dcm"))
                    )
                )
                .Returns(ExtractedFileStatus.Anonymised);

            var statusExchange = globals.DicomAnonymiserOptions.ExtractFileStatusProducerOptions.ExchangeName;
            var successQueue = globals.IsIdentifiableOptions.QueueName;
            var failureQueue = globals.CohortPackagerOptions.NoVerifyStatusOptions.QueueName;

            using (
                var tester = new MicroserviceTester(
                    globals.RabbitOptions,
                    globals.DicomAnonymiserOptions.AnonFileConsumerOptions
                )
            )
            {
                tester.CreateExchange(statusExchange, successQueue, isSecondaryBinding: false, routingKey: "verify");
                tester.CreateExchange(statusExchange, failureQueue, isSecondaryBinding: true, routingKey: "noverify");

                tester.SendMessage(globals.DicomAnonymiserOptions.AnonFileConsumerOptions, new MessageHeader(), testExtractFileMessage);

                var host = new DicomAnonymiserHost(globals, mockAnonymiser.Object, mockFs);
                host.Start();

                List<ExtractedFileStatusMessage> statusMessages = new();
                var timeoutSecs = 10;

                while (statusMessages.Count == 0 && timeoutSecs > 0)
                {
                    statusMessages.AddRange(tester.ConsumeMessages<ExtractedFileStatusMessage>(successQueue).Select(x => x.Item2));
                    statusMessages.AddRange(tester.ConsumeMessages<ExtractedFileStatusMessage>(failureQueue).Select(x => x.Item2));

                    --timeoutSecs;
                    if (statusMessages.Count == 0)
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                }

                host.Stop("Test end");

                var statusMessage = statusMessages.Single();
                Assert.AreEqual(ExtractedFileStatus.Anonymised, statusMessage.Status, statusMessage.StatusMessage);
                // assert other properties
            }
        }

        #endregion
    }
}