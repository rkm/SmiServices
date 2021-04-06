﻿using Moq;
using NUnit.Framework;
using Smi.Common.Helpers;
using Smi.Common.Messages;
using Smi.Common.Messages.Extraction;
using Smi.Common.Messaging;
using Smi.Common.Options;
using Smi.Common.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;


namespace Applications.ExtractionLauncher.Tests
{
    public class ExtractionMessageSenderTests
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

        private class TestConsoleInput : IConsoleInput
        {
            private string _line;

            public TestConsoleInput(string line)
            {
                _line = line;
            }

            public string GetNextLine()
            {
                string line = _line;
                _line = null;
                return line;
            }
        }

        #endregion

        #region Tests

        [TestCase(true)]
        [TestCase(false)]
        public void HappyPath_Interactive(bool confirm)
        {
            Expression<Func<IProducerModel, IMessageHeader>> expr = x => x.SendMessage(It.IsAny<IMessage>(), null, It.IsAny<string>());

            var mockExtractionRequestProducer = new Mock<IProducerModel>(MockBehavior.Strict);
            mockExtractionRequestProducer.Setup(expr).Returns((IMessageHeader)null);

            var mockExtractionRequestInfoProducer = new Mock<IProducerModel>(MockBehavior.Strict);
            mockExtractionRequestInfoProducer.Setup(expr).Returns((IMessageHeader)null);

            var processor = new ExtractionMessageSender(
                new ExtractionLauncherOptions(),
                new ExtractionLauncherCliOptions { ProjectId = "1234-5678" },
                mockExtractionRequestProducer.Object,
                mockExtractionRequestInfoProducer.Object,
                "extractDir",
                new TestDateTimeProvider(),
                new TestConsoleInput(confirm ? "y" : "n")
            );

            var idList = new List<string> { "foo" };
            processor.SendMessages(ExtractionKey.StudyInstanceUID, idList);

            if (confirm)
            {
                mockExtractionRequestProducer.Verify(expr, Times.Once);
                mockExtractionRequestInfoProducer.Verify(expr, Times.Once);
            }
            else
            {
                mockExtractionRequestProducer.Verify(expr, Times.Never);
                mockExtractionRequestInfoProducer.Verify(expr, Times.Never);
            }
        }

        [Test]
        public void HappyPath_NonInteractive()
        {
            Expression<Func<IProducerModel, IMessageHeader>> expr = x => x.SendMessage(It.IsAny<IMessage>(), null, It.IsAny<string>());

            var mockExtractionRequestProducer = new Mock<IProducerModel>(MockBehavior.Strict);
            mockExtractionRequestProducer.Setup(expr).Returns((IMessageHeader)null);

            var mockExtractionRequestInfoProducer = new Mock<IProducerModel>(MockBehavior.Strict);
            mockExtractionRequestInfoProducer.Setup(expr).Returns((IMessageHeader)null);

            var processor = new ExtractionMessageSender(
                new ExtractionLauncherOptions(),
                new ExtractionLauncherCliOptions { ProjectId = "1234-5678", NonInteractive = true },
                mockExtractionRequestProducer.Object,
                mockExtractionRequestInfoProducer.Object,
                "extractDir",
                new TestDateTimeProvider(),
                new RealConsoleInput()
            );

            var idList = new List<string> { "foo" };
            processor.SendMessages(ExtractionKey.StudyInstanceUID, idList);

            mockExtractionRequestProducer.Verify(expr, Times.Once);
            mockExtractionRequestInfoProducer.Verify(expr, Times.Once);
        }

        [TestCase(null)]
        [TestCase("  ")]
        public void ExtractionDir_IsValidated(string? extractionDir)
        {
            var exc = Assert.Throws<ArgumentException>(() =>
            {
                var _ = new ExtractionMessageSender(
                    new ExtractionLauncherOptions(),
                    new ExtractionLauncherCliOptions(),
                    new Mock<IProducerModel>(MockBehavior.Loose).Object,
                    new Mock<IProducerModel>(MockBehavior.Loose).Object,
                    extractionDir,
                    new TestDateTimeProvider(),
                    new RealConsoleInput()
                );
            });
            Assert.AreEqual("extractionDir", exc.Message);
        }

        [TestCase(null)]
        [TestCase("  ")]
        public void ProjectId_IsValidated(string? projectId)
        {
            var exc = Assert.Throws<ArgumentException>(() =>
            {
                var _ = new ExtractionMessageSender(
                    new ExtractionLauncherOptions(),
                    new ExtractionLauncherCliOptions { ProjectId = projectId },
                    new Mock<IProducerModel>(MockBehavior.Loose).Object,
                    new Mock<IProducerModel>(MockBehavior.Loose).Object,
                    "extractDir",
                    new TestDateTimeProvider(),
                    new RealConsoleInput()
                );
            });
            Assert.AreEqual("ProjectId", exc.Message);
        }

        [Test]
        public void MaxIdentifiersPerMessage_IsValidated()
        {
            var exc = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var _ = new ExtractionMessageSender(
                    new ExtractionLauncherOptions { MaxIdentifiersPerMessage = 0 },
                    new ExtractionLauncherCliOptions(),
                    new Mock<IProducerModel>(MockBehavior.Loose).Object,
                    new Mock<IProducerModel>(MockBehavior.Loose).Object,
                    "extractDir",
                    new TestDateTimeProvider(),
                    new RealConsoleInput()
                );
            });
            Assert.True(exc.Message.EndsWith("(Parameter 'MaxIdentifiersPerMessage')"));
        }


        [Test]
        public void IdList_IsNotEmpty()
        {
            var sender = new ExtractionMessageSender(
                new ExtractionLauncherOptions(),
                new ExtractionLauncherCliOptions {ProjectId = "1234-5678"},
                new Mock<IProducerModel>(MockBehavior.Loose).Object,
                new Mock<IProducerModel>(MockBehavior.Loose).Object,
                "extractDir",
                new TestDateTimeProvider(),
                new RealConsoleInput()
            );

            var exc = Assert.Throws<ArgumentException>(() =>
            {
                sender.SendMessages(ExtractionKey.StudyInstanceUID, new List<string>());
            });
            Assert.AreEqual("ID list is empty", exc.Message);
        }


        [TestCase(1, 1, 1)] // nIds = maxPerMessage  => 1 message
        [TestCase(1, 10, 1)] // nIds < maxPerMessage => 1 message
        [TestCase(2, 1, 2)] // nIds > maxPerMessage => 2 messages
        public void ListChunking_EdgeCases(int nIds, int maxPerMessage, int expectedMessages)
        {
            Expression<Func<IProducerModel, IMessageHeader>> expr = x => x.SendMessage(It.IsAny<IMessage>(), null, It.IsAny<string>());

            var mockExtractionRequestProducer = new Mock<IProducerModel>(MockBehavior.Strict);
            mockExtractionRequestProducer.Setup(expr).Returns((IMessageHeader)null);

            var mockExtractionRequestInfoProducer = new Mock<IProducerModel>(MockBehavior.Strict);
            mockExtractionRequestInfoProducer.Setup(expr).Returns((IMessageHeader)null);

            var processor = new ExtractionMessageSender(
                new ExtractionLauncherOptions { MaxIdentifiersPerMessage = maxPerMessage },
                new ExtractionLauncherCliOptions { ProjectId = "1234-5678", NonInteractive = true },
                mockExtractionRequestProducer.Object,
                mockExtractionRequestInfoProducer.Object,
                "extractDir",
                new TestDateTimeProvider(),
                new RealConsoleInput()
            );

            List<string> idList = Enumerable.Range(0, nIds).Select(x => x.ToString()).ToList();
            processor.SendMessages(ExtractionKey.StudyInstanceUID, idList);

            mockExtractionRequestProducer.Verify(expr, Times.Exactly(expectedMessages));
            mockExtractionRequestInfoProducer.Verify(expr, Times.Once);
        }

        #endregion
    }
}
