
using NUnit.Framework;
using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Microservices.DicomTagReader.Execution;
using Moq;
using Smi.Common.Messages;
using Smi.Common.Tests;
using System.IO.Compression;
using System.Collections.Generic;

namespace Microservices.DicomTagReader.Tests.Execution
{
    //TODO Some of these can be tested without RabbitMQ
    [TestFixture, RequiresRabbit]
    public class TagReaderTests
    {
        private readonly DicomTagReaderTestHelper _helper = new();

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _helper.SetUpSuite();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _helper.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            _helper.ResetSuite();
        }

        [TearDown]
        public void TearDown()
        {

        }


        /// <summary>
        /// Test that the TagReader behaves properly depending on the NackIfAnyFileErrors option
        /// </summary>
        /// <param name="nackIfAnyFileErrors"></param>
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void TestNackIfAnyFileErrorsOption(bool nackIfAnyFileErrors)
        {
            var messagesSent = 0;

            var fi = new FileInfo(Path.Combine(_helper.TestDir.FullName, "InvalidFile.dcm"));
            File.WriteAllBytes(fi.FullName, new byte[] { 0x12, 0x34, 0x56, 0x78 });

            Assert.AreEqual(2,_helper.TestDir.EnumerateFiles("*.dcm").Count());

            _helper.Options.DicomTagReaderOptions.NackIfAnyFileErrors = nackIfAnyFileErrors;
            _helper.Options.FileSystemOptions.FileSystemRoot = _helper.TestDir.FullName;
            _helper.TestAccessionDirectoryMessage.DirectoryPath = _helper.TestDir.FullName;

            _helper.TestImageModel
                .Setup(x => x.SendMessage(It.IsAny<IMessage>(), It.IsAny<MessageHeader>(), It.IsAny<string>()))
                .Returns(new MessageHeader());

            _helper.TestSeriesModel
                .Setup(x => x.SendMessage(It.IsAny<IMessage>(), It.IsAny<MessageHeader>(), It.IsAny<string>()))
                .Returns(new MessageHeader())
                .Callback(() => ++messagesSent);

            var tagReader = new SerialTagReader(_helper.Options.DicomTagReaderOptions, _helper.Options.FileSystemOptions, _helper.TestSeriesModel.Object, _helper.TestImageModel.Object, new FileSystem());

            if (nackIfAnyFileErrors)
            {
                Assert.Throws<ApplicationException>(() => tagReader.ReadTags(new MessageHeader(), _helper.TestAccessionDirectoryMessage));
                Assert.True(messagesSent == 0);
            }
            else
            {
                tagReader.ReadTags(new MessageHeader(), _helper.TestAccessionDirectoryMessage);
                Assert.True(messagesSent == 1);
            }
        }

        /// <summary>
        /// Tests that a directory path to search outside of the FileSystemRoot is rejected
        /// </summary>
        [Test]
        public void TestPathNotBelowRootThrowsException()
        {
            _helper.Options.FileSystemOptions.FileSystemRoot = "C:\\Temp";
            _helper.TestAccessionDirectoryMessage.DirectoryPath = _helper.TestDir.FullName;

            var tagReader = new SerialTagReader(_helper.Options.DicomTagReaderOptions, _helper.Options.FileSystemOptions, _helper.TestSeriesModel.Object, _helper.TestImageModel.Object, new FileSystem());

            Assert.Throws<ApplicationException>(() => tagReader.ReadTags(null, _helper.TestAccessionDirectoryMessage));
        }

        /// <summary>
        /// Tests that a directory containing no dicom files is rejected
        /// </summary>
        [Test]
        public void TestEmptyDirectoryThrowsException()
        {
            foreach (FileInfo file in _helper.TestDir.EnumerateFiles("*.dcm"))
                file.Delete();

            Assert.True(!_helper.TestDir.EnumerateFiles("*.dcm").Any());

            _helper.Options.FileSystemOptions.FileSystemRoot = _helper.TestDir.FullName;
            _helper.TestAccessionDirectoryMessage.DirectoryPath = _helper.TestDir.FullName;

            var tagReader = new SerialTagReader(_helper.Options.DicomTagReaderOptions, _helper.Options.FileSystemOptions, _helper.TestSeriesModel.Object, _helper.TestImageModel.Object, new FileSystem());

            Assert.Throws<ApplicationException>(() => tagReader.ReadTags(null, _helper.TestAccessionDirectoryMessage));
        }

        /// <summary>
        /// Tests that the field ImagesInSeries of the SeriesMessage is set properly
        /// </summary>
        [Test]
        public void TestSeriesMessageImagesInSeriesCorrect()
        {
            _helper.Options.FileSystemOptions.FileSystemRoot = _helper.TestDir.FullName;
            _helper.TestAccessionDirectoryMessage.DirectoryPath = _helper.TestDir.FullName;

            File.Copy($"{_helper.TestDir.FullName}/MyTestFile.dcm", $"{_helper.TestDir.FullName}/MyTestFile2.dcm");
            Assert.True(_helper.TestDir.EnumerateFiles("*.dcm").Count() == 2);

            IMessage message = null;

            _helper.TestImageModel
                .Setup(x => x.SendMessage(It.IsAny<IMessage>(), It.IsAny<IMessageHeader>(), It.IsAny<string>()))
                .Returns(new MessageHeader());

            _helper.TestSeriesModel
                .Setup(x => x.SendMessage(It.IsAny<IMessage>(), It.IsAny<IMessageHeader>(), It.IsAny<string>()))
                .Callback<IMessage, IMessageHeader, string>((m, h, s) => message = m)
                .Returns(new MessageHeader());

            var tagReader = new SerialTagReader(_helper.Options.DicomTagReaderOptions, _helper.Options.FileSystemOptions, _helper.TestSeriesModel.Object, _helper.TestImageModel.Object, new FileSystem());
            tagReader.ReadTags(new MessageHeader(), _helper.TestAccessionDirectoryMessage);

            Assert.True(message != null);

            var seriesMessage = message as SeriesMessage;
            Assert.True(seriesMessage != null);
            Assert.True(seriesMessage.ImagesInSeries == 2);
        }

        /// <summary>
        /// Tests that we can read a mixture of zip files and dcm files using <see cref="TagReaderBase"/>
        /// </summary>
        [Test]
        public void TestSeriesMessageImagesInSeriesCorrect_WhenUsingZips()
        {
            _helper.Options.FileSystemOptions.FileSystemRoot = _helper.TestDir.FullName;
            _helper.TestAccessionDirectoryMessage.DirectoryPath = _helper.TestDir.FullName;

            File.Copy($"{_helper.TestDir.FullName}/MyTestFile.dcm", $"{_helper.TestDir.FullName}/MyTestFile2.dcm");
            Assert.True(_helper.TestDir.EnumerateFiles("*.dcm").Count() == 2);

            // Where we want to put it
            var zipFilePath = Path.Combine(_helper.TestDir.FullName,"my.zip");

            //create the zip file in a temporary directory outside of the working directory to avoid file access errors
            var tempDir = _helper.TestDir.Parent.CreateSubdirectory("temppp");
            var tempPath = Path.Combine(tempDir.FullName,"my.zip");
                        
            //zip everything in the working dir to the temp path zip file
            ZipFile.CreateFromDirectory(_helper.TestDir.FullName,tempPath);

            //then move the zip file where we actually want it (in the working path)
            File.Move(tempPath,zipFilePath);
            
            Assert.True(_helper.TestDir.EnumerateFiles("*.dcm").Count() == 2);
            Assert.True(_helper.TestDir.EnumerateFiles("*.zip").Count() == 1);

            IMessage message = null;
            List<IMessage> fileImages = new();

            _helper.TestImageModel
                .Setup(x => x.SendMessage(It.IsAny<IMessage>(), It.IsAny<IMessageHeader>(), It.IsAny<string>()))
                .Callback<IMessage, IMessageHeader, string>((m, h, s) => fileImages.Add(m))
                .Returns(new MessageHeader());

            _helper.TestSeriesModel
                .Setup(x => x.SendMessage(It.IsAny<IMessage>(), It.IsAny<IMessageHeader>(), It.IsAny<string>()))
                .Callback<IMessage, IMessageHeader, string>((m, h, s) => message = m)
                .Returns(new MessageHeader());

            var tagReader = new SerialTagReader(_helper.Options.DicomTagReaderOptions, _helper.Options.FileSystemOptions, _helper.TestSeriesModel.Object, _helper.TestImageModel.Object, new FileSystem());
            tagReader.ReadTags(new MessageHeader(), _helper.TestAccessionDirectoryMessage);

            Assert.True(message != null);

            var seriesMessage = message as SeriesMessage;
            Assert.True(seriesMessage != null);
            
            Assert.True(seriesMessage.ImagesInSeries == 4, "Expected 4, 2 in the zip archive and 2 in the root");

            Assert.AreEqual(4,fileImages.Count,"Expected 4 file messages to be sent and recorded by TestImageModel Callback");

            Assert.Contains("MyTestFile.dcm",fileImages.Select(m=>((DicomFileMessage)m).DicomFilePath).ToArray());
            Assert.Contains("MyTestFile2.dcm",fileImages.Select(m=>((DicomFileMessage)m).DicomFilePath).ToArray());
            Assert.Contains("my.zip!MyTestFile.dcm",fileImages.Select(m=>((DicomFileMessage)m).DicomFilePath).ToArray());
            Assert.Contains("my.zip!MyTestFile2.dcm",fileImages.Select(m=>((DicomFileMessage)m).DicomFilePath).ToArray());
        }

        /// <summary>
        /// Tests that the correct exception is thrown if we try and open a corrupt dicom file
        /// </summary>
        [Test]
        public void TestInvalidFileThrowsApplicationException()
        {
            _helper.Options.DicomTagReaderOptions.NackIfAnyFileErrors = true;
            _helper.Options.FileSystemOptions.FileSystemRoot = _helper.TestDir.FullName;
            _helper.TestAccessionDirectoryMessage.DirectoryPath = _helper.TestDir.FullName;

            var fi = new FileInfo(Path.Combine(_helper.TestDir.FullName, "InvalidFile.dcm"));
            File.WriteAllBytes(fi.FullName, new byte[] { 0x12, 0x34, 0x56, 0x78 });

            // One valid, one invalid
            Assert.AreEqual(2,_helper.TestDir.EnumerateFiles("*.dcm").Count());

            var tagReader = new SerialTagReader(_helper.Options.DicomTagReaderOptions, _helper.Options.FileSystemOptions, _helper.TestSeriesModel.Object, _helper.TestImageModel.Object, new FileSystem());

            Assert.Throws<ApplicationException>(() => tagReader.ReadTags(null, _helper.TestAccessionDirectoryMessage));
        }
    }
}
