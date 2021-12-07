using Microservices.DicomAnonymiser.Anonymisers;
using Moq;
using NUnit.Framework;
using RabbitMQ.Client;
using Smi.Common.Events;
using Smi.Common.Messages;
using Smi.Common.Messages.Extraction;
using Smi.Common.Messaging;
using Smi.Common.Options;
using Smi.Common.Tests;
using Smi.Common.Tests.Messaging;
using System;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq.Expressions;

namespace Microservices.DicomAnonymiser.Tests
{
    public class DicomAnonymiserConsumerTests
    {
        #region Fixture Methods

        private ExtractFileMessage _extractFileMessage;
        private DicomAnonymiserOptions _options;
        private Mock<IModel> _mockModel;

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
        public void SetUp()
        {
            _extractFileMessage = new ExtractFileMessage
            {
                JobSubmittedAt = DateTime.UtcNow,
                ExtractionJobIdentifier = Guid.NewGuid(),
                ProjectNumber = "1234",
                ExtractionDirectory = "extractDir",
                DicomFilePath = "foo.dcm",
                OutputPath = "foo-an.dcm",
            };

            _options = new DicomAnonymiserOptions();
            _options.RoutingKeySuccess = "yay";
            _options.RoutingKeyFailure = "nay";
            _options.FailIfSourceWriteable = true;

            _mockModel = new Mock<IModel>(MockBehavior.Strict);
            _mockModel.Setup(x => x.IsClosed).Returns(false);
            _mockModel.Setup(x => x.BasicAck(It.IsAny<ulong>(), It.IsAny<bool>()));
            _mockModel.Setup(x => x.BasicNack(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>()));
        }

        [TearDown]
        public void TearDown() { }

        #endregion

        #region Tests

        [Test]
        public void Constructor_MissingFileSystemRoots_ThrowsException()
        {
            var mockFs = new MockFileSystem();
            var dicomRoot = mockFs.Directory.CreateDirectory("dicom");
            var extractRoot = mockFs.Directory.CreateDirectory("extract");

            Assert.Throws<ArgumentNullException>(() =>
            {
                new DicomAnonymiserConsumer(
                    _options,
                    fileSystemRoot: null,
                    extractRoot.FullName,
                    new Mock<IDicomAnonymiser>(MockBehavior.Strict).Object,
                    new Mock<IProducerModel>(MockBehavior.Strict).Object,
                    mockFs
                );
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                new DicomAnonymiserConsumer(
                    new DicomAnonymiserOptions(),
                    dicomRoot.FullName,
                    extractRoot: null,
                    new Mock<IDicomAnonymiser>(MockBehavior.Strict).Object,
                    new Mock<IProducerModel>(MockBehavior.Strict).Object,
                    mockFs
                );
            });
        }

        [Test]
        public void ProcessMessageImpl_HappyPath()
        {
            var mockFs = new MockFileSystem();
            var dicomRoot = mockFs.Directory.CreateDirectory("dicom");
            var extractRoot = mockFs.Directory.CreateDirectory("extract");
            var extractDir = mockFs.Path.Combine(extractRoot.FullName, _extractFileMessage.ExtractionDirectory);
            mockFs.Directory.CreateDirectory(extractDir);
            var fakeDicom = mockFs.Path.Combine(dicomRoot.FullName, "foo.dcm");
            mockFs.File.Create(fakeDicom);
            mockFs.File.SetAttributes(fakeDicom, mockFs.File.GetAttributes(fakeDicom) | FileAttributes.ReadOnly);

            var mockAnonymiser = new Mock<IDicomAnonymiser>(MockBehavior.Strict);
            Expression<Func<IDicomAnonymiser, ExtractedFileStatus>> expectedAnonCall =
                x => x.Anonymise(
                    It.Is<IFileInfo>(x => x.FullName == fakeDicom),
                    It.Is<IFileInfo>(x => x.FullName == mockFs.Path.Combine(extractDir, _extractFileMessage.OutputPath))
                );
            mockAnonymiser
                .Setup(expectedAnonCall)
                .Returns(ExtractedFileStatus.Anonymised);

            Expression<Func<IProducerModel, IMessageHeader>> expectedSendCall =
                x => x.SendMessage(
                    It.Is<ExtractedFileStatusMessage>(x =>
                        x.Status == ExtractedFileStatus.Anonymised &&
                        x.StatusMessage == null &&
                        x.OutputFilePath == _extractFileMessage.OutputPath
                     ),
                    It.IsAny<IMessageHeader>(),
                    _options.RoutingKeySuccess
                );
            var mockProducerModel = new Mock<IProducerModel>();
            mockProducerModel.Setup(expectedSendCall);


            var consumer = new DicomAnonymiserConsumer(
                _options,
                dicomRoot.FullName,
                extractRoot.FullName,
                mockAnonymiser.Object,
                mockProducerModel.Object,
                mockFs
            );

            var testDeliverArgs = ConsumerTestHelpers.GetMockDeliverArgs(_extractFileMessage);
            consumer.SetModel(_mockModel.Object);

            var fatalCalled = false;
            consumer.OnFatal += (sender, _) => fatalCalled = true;

            consumer.ProcessMessage(testDeliverArgs);

            new TestTimelineAwaiter().Await(() => consumer.AckCount == 1 && consumer.NackCount == 0, "", 5_000);

            mockAnonymiser.Verify(expectedAnonCall, Times.Once);
            mockProducerModel.Verify(expectedSendCall, Times.Once);
        }

        [Test]
        public void ProcessMessageImpl_IsIdentifiableExtraction_ThrowsException()
        {
            var mockFs = new MockFileSystem();
            var dicomRoot = mockFs.Directory.CreateDirectory("dicom");
            var extractRoot = mockFs.Directory.CreateDirectory("extract");
            mockFs.Directory.CreateDirectory(mockFs.Path.Combine(extractRoot.FullName, _extractFileMessage.ExtractionDirectory));
            var fakeDicom = mockFs.Path.Combine(dicomRoot.FullName, "foo.dcm");
            mockFs.File.Create(fakeDicom);
            mockFs.File.SetAttributes(fakeDicom, mockFs.File.GetAttributes(fakeDicom) | FileAttributes.ReadOnly);

            var consumer = new DicomAnonymiserConsumer(
                _options,
                dicomRoot.FullName,
                extractRoot.FullName,
                new Mock<IDicomAnonymiser>(MockBehavior.Strict).Object,
                new Mock<IProducerModel>(MockBehavior.Strict).Object,
                mockFs
            );

            _extractFileMessage.IsIdentifiableExtraction = true;

            var testDeliverArgs = ConsumerTestHelpers.GetMockDeliverArgs(_extractFileMessage);
            consumer.SetModel(_mockModel.Object);

            FatalErrorEventArgs fatalArgs = null;
            consumer.OnFatal += (_, args) => fatalArgs = args;

            consumer.ProcessMessage(testDeliverArgs);

            new TestTimelineAwaiter().Await(() => fatalArgs != null, "Expected Fatal to be called");
            Assert.AreEqual("ProcessMessageImpl threw unhandled exception", fatalArgs.Message);
            Assert.AreEqual("DicomAnonymiserConsumer should not handle identifiable extraction messages", fatalArgs.Exception.Message);
            Assert.AreEqual(0, consumer.AckCount);
            Assert.AreEqual(0, consumer.NackCount);
        }

        [Test]
        public void ProcessMessageImpl_SourceFileMissing_AcksWithFailureStatus()
        {
            var mockFs = new MockFileSystem();
            var dicomRoot = mockFs.Directory.CreateDirectory("dicom");
            var extractRoot = mockFs.Directory.CreateDirectory("extract");
            mockFs.Directory.CreateDirectory(mockFs.Path.Combine(extractRoot.FullName, _extractFileMessage.ExtractionDirectory));
            var fakeDicom = mockFs.Path.Combine(dicomRoot.FullName, "foo.dcm");
            mockFs.File.Create(fakeDicom);
            mockFs.File.SetAttributes(fakeDicom, mockFs.File.GetAttributes(fakeDicom) | FileAttributes.ReadOnly);

            Expression<Func<IProducerModel, IMessageHeader>> expectedCall =
                x => x.SendMessage(
                    It.Is<ExtractedFileStatusMessage>(x =>
                        x.Status == ExtractedFileStatus.FileMissing &&
                        x.StatusMessage == $"Could not find file to anonymise: '{fakeDicom}'" &&
                        x.OutputFilePath == null
                     ),
                    It.IsAny<IMessageHeader>(),
                    _options.RoutingKeyFailure
                );
            var mockProducerModel = new Mock<IProducerModel>();
            mockProducerModel.Setup(expectedCall);

            var consumer = new DicomAnonymiserConsumer(
                _options,
                dicomRoot.FullName,
                extractRoot.FullName,
                new Mock<IDicomAnonymiser>(MockBehavior.Strict).Object,
                mockProducerModel.Object,
                mockFs
            );

            mockFs.File.SetAttributes(fakeDicom, mockFs.File.GetAttributes(fakeDicom) & ~FileAttributes.ReadOnly);
            mockFs.File.Delete(fakeDicom);

            var testDeliverArgs = ConsumerTestHelpers.GetMockDeliverArgs(_extractFileMessage);
            consumer.SetModel(_mockModel.Object);

            consumer.ProcessMessage(testDeliverArgs);

            new TestTimelineAwaiter().Await(() => consumer.AckCount == 1 && consumer.NackCount == 0);

            mockProducerModel.Verify(expectedCall, Times.Once);
        }

        [Test]
        public void ProcessMessageImpl_FailIfSourceWriteable_AcksWithFailureStatus()
        {
            var mockFs = new MockFileSystem();
            var dicomRoot = mockFs.Directory.CreateDirectory("dicom");
            var extractRoot = mockFs.Directory.CreateDirectory("extract");
            mockFs.Directory.CreateDirectory(mockFs.Path.Combine(extractRoot.FullName, _extractFileMessage.ExtractionDirectory));
            var fakeDicom = mockFs.Path.Combine(dicomRoot.FullName, "foo.dcm");
            mockFs.File.Create(fakeDicom);
            mockFs.File.SetAttributes(fakeDicom, mockFs.File.GetAttributes(fakeDicom) | FileAttributes.ReadOnly);

            Expression<Func<IProducerModel, IMessageHeader>> expectedCall =
                x => x.SendMessage(
                    It.Is<ExtractedFileStatusMessage>(x =>
                        x.Status == ExtractedFileStatus.ErrorWontRetry &&
                        x.StatusMessage == $"Source file was writeable and FailIfSourceWriteable is set: '{fakeDicom}'" &&
                        x.OutputFilePath == null
                     ),
                    It.IsAny<IMessageHeader>(),
                    _options.RoutingKeyFailure
                );
            var mockProducerModel = new Mock<IProducerModel>();
            mockProducerModel.Setup(expectedCall);

            var consumer = new DicomAnonymiserConsumer(
                _options,
                dicomRoot.FullName,
                extractRoot.FullName,
                new Mock<IDicomAnonymiser>(MockBehavior.Strict).Object,
                mockProducerModel.Object,
                mockFs
            );

            mockFs.File.SetAttributes(fakeDicom, mockFs.File.GetAttributes(fakeDicom) & ~FileAttributes.ReadOnly);

            var testDeliverArgs = ConsumerTestHelpers.GetMockDeliverArgs(_extractFileMessage);
            consumer.SetModel(_mockModel.Object);

            consumer.ProcessMessage(testDeliverArgs);

            new TestTimelineAwaiter().Await(() => consumer.AckCount == 1 && consumer.NackCount == 0);

            mockProducerModel.Verify(expectedCall, Times.Once);
        }

        [Test]
        public void ProcessMessageImpl_ExtractionDirMissing_ThrowsException()
        {
            var mockFs = new MockFileSystem();
            var dicomRoot = mockFs.Directory.CreateDirectory("dicom");
            var extractRoot = mockFs.Directory.CreateDirectory("extract");
            var extractDir = mockFs.Path.Combine(extractRoot.FullName, _extractFileMessage.ExtractionDirectory);
            mockFs.Directory.CreateDirectory(extractDir);
            var fakeDicom = mockFs.Path.Combine(dicomRoot.FullName, "foo.dcm");
            mockFs.File.Create(fakeDicom);
            mockFs.File.SetAttributes(fakeDicom, mockFs.File.GetAttributes(fakeDicom) | FileAttributes.ReadOnly);

            var consumer = new DicomAnonymiserConsumer(
                _options,
                dicomRoot.FullName,
                extractRoot.FullName,
                new Mock<IDicomAnonymiser>(MockBehavior.Strict).Object,
                new Mock<IProducerModel>(MockBehavior.Strict).Object,
                mockFs
            );

            mockFs.Directory.Delete(extractDir);

            var testDeliverArgs = ConsumerTestHelpers.GetMockDeliverArgs(_extractFileMessage);
            consumer.SetModel(_mockModel.Object);

            FatalErrorEventArgs fatalArgs = null;
            consumer.OnFatal += (_, args) => fatalArgs = args;

            consumer.ProcessMessage(testDeliverArgs);

            new TestTimelineAwaiter().Await(() => fatalArgs != null, "Expected Fatal to be called", 5_000);
            Assert.AreEqual("ProcessMessageImpl threw unhandled exception", fatalArgs.Message);
            Assert.AreEqual($"Expected extraction directory to exist: '{extractDir}'", fatalArgs.Exception.Message);
            Assert.AreEqual(0, consumer.AckCount);
            Assert.AreEqual(0, consumer.NackCount);
        }

        [Test]
        public void ProcessMessageImpl_AnonymisationFailed_AcksWithFailureStatus()
        {
            var mockFs = new MockFileSystem();
            var dicomRoot = mockFs.Directory.CreateDirectory("dicom");
            var extractRoot = mockFs.Directory.CreateDirectory("extract");
            mockFs.Directory.CreateDirectory(mockFs.Path.Combine(extractRoot.FullName, _extractFileMessage.ExtractionDirectory));
            var fakeDicom = mockFs.Path.Combine(dicomRoot.FullName, "foo.dcm");
            mockFs.File.Create(fakeDicom);
            mockFs.File.SetAttributes(fakeDicom, mockFs.File.GetAttributes(fakeDicom) | FileAttributes.ReadOnly);

            var mockAnonymiser = new Mock<IDicomAnonymiser>(MockBehavior.Strict);
            mockAnonymiser
                .Setup(x => x.Anonymise(It.IsAny<IFileInfo>(), It.IsAny<IFileInfo>()))
                .Throws(new Exception("oh no"));

            Expression<Func<IProducerModel, IMessageHeader>> expectedCall =
                x => x.SendMessage(
                    It.Is<ExtractedFileStatusMessage>(x =>
                        x.Status == ExtractedFileStatus.ErrorWontRetry &&
                        x.StatusMessage == "oh no" &&
                        x.OutputFilePath == null
                     ),
                    It.IsAny<IMessageHeader>(),
                    _options.RoutingKeyFailure
                );
            var mockProducerModel = new Mock<IProducerModel>();
            mockProducerModel.Setup(expectedCall);

            var consumer = new DicomAnonymiserConsumer(
                _options,
                dicomRoot.FullName,
                extractRoot.FullName,
                mockAnonymiser.Object,
                mockProducerModel.Object,
                mockFs
            );

            var testDeliverArgs = ConsumerTestHelpers.GetMockDeliverArgs(_extractFileMessage);
            consumer.SetModel(_mockModel.Object);

            consumer.ProcessMessage(testDeliverArgs);

            new TestTimelineAwaiter().Await(() => consumer.AckCount == 1 && consumer.NackCount == 0);

            mockProducerModel.Verify(expectedCall, Times.Once);
        }

        #endregion
    }
}
