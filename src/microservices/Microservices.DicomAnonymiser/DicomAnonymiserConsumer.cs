using Microservices.DicomAnonymiser.Anonymisers;
using NLog;
using Smi.Common.Messages;
using Smi.Common.Messages.Extraction;
using Smi.Common.Messaging;
using Smi.Common.Options;
using System;
using System.IO;
using System.IO.Abstractions;

namespace Microservices.DicomAnonymiser
{
    public class DicomAnonymiserConsumer : Consumer<ExtractFileMessage>
    {
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        private readonly DicomAnonymiserOptions _options;
        private readonly IFileSystem _fileSystem;
        private readonly string _fileSystemRoot;
        private readonly string _extractRoot;
        private readonly IDicomAnonymiser _anonymiser;
        private readonly IProducerModel _statusMessageProducer;

        public DicomAnonymiserConsumer(
            DicomAnonymiserOptions options,
            string fileSystemRoot,
            string extractRoot,
            IDicomAnonymiser anonymiser,
            IProducerModel statusMessageProducer,
            IFileSystem fileSystem = null
        )
        {
            _options = options;
            _fileSystem = fileSystem ?? new FileSystem();
            _fileSystemRoot = fileSystemRoot;
            _extractRoot = extractRoot;
            _anonymiser = anonymiser;
            _statusMessageProducer = statusMessageProducer;

            if (!_fileSystem.Directory.Exists(fileSystemRoot))
                throw new Exception($"Filesystem root does not exist: '{fileSystemRoot}'");

            if (!_fileSystem.Directory.Exists(extractRoot))
                throw new Exception($"Extract root does not exist: '{extractRoot}'");
        }

        protected override void ProcessMessageImpl(IMessageHeader header, ExtractFileMessage message, ulong tag)
        {
            if (message.IsIdentifiableExtraction)
                throw new Exception("DicomAnonymiserConsumer should not handle identifiable extraction messages");

            var statusMessage = new ExtractedFileStatusMessage(message);

            var sourceFileAbs = _fileSystem.FileInfo.FromFileName(_fileSystem.Path.Combine(_fileSystemRoot, message.DicomFilePath));

            if (!sourceFileAbs.Exists)
            {
                statusMessage.StatusMessage = $"Could not find file to anonymise ('{message.DicomFilePath}')";
                statusMessage.OutputFilePath = "";
                statusMessage.Status = ExtractedFileStatus.FileMissing;

                _statusMessageProducer.SendMessage(statusMessage, header, _options.RoutingKeyFailure);
                Ack(header, tag);
                return;
            }

            if (_options.FailIfSourceWriteable && sourceFileAbs.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                statusMessage.StatusMessage = $"Source file was writeable and FailIfSourceWriteable is set ('{message.DicomFilePath}')";
                statusMessage.OutputFilePath = "";
                statusMessage.Status = ExtractedFileStatus.ErrorWontRetry;

                _statusMessageProducer.SendMessage(statusMessage, header, _options.RoutingKeyFailure);
                Ack(header, tag);
                return;
            }

            var extractionDirAbs = _fileSystem.Path.Combine(_extractRoot, message.ExtractionDirectory);

            // NOTE(rkm 2021-12-07) Since this direcotry shold have already been created, we treat this more like an assertion and throw if not found.
            // This helps prevent a flood of messages if e.g. the filesystem is temporarily unavialable
            if (!_fileSystem.Directory.Exists(extractionDirAbs))
                throw new DirectoryNotFoundException($"Expected extraction directory to exist: '{extractionDirAbs}'");

            var destFileAbs = _fileSystem.FileInfo.FromFileName(_fileSystem.Path.Combine(extractionDirAbs, message.OutputPath));

            destFileAbs.Directory.Create();

            string routingKey;

            _logger.Debug($"Anonymising '{sourceFileAbs}' to '{destFileAbs}'");

            try
            {
                _anonymiser.Anonymise(sourceFileAbs, destFileAbs);
                _logger.Debug($"Anonymisation of '{sourceFileAbs}' successful");

                statusMessage.StatusMessage = "";
                statusMessage.OutputFilePath = message.OutputPath;
                statusMessage.Status = ExtractedFileStatus.Anonymised;
                routingKey = _options.RoutingKeySuccess;
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Error anonymising '{sourceFileAbs}'");

                statusMessage.StatusMessage = e.Message;
                statusMessage.OutputFilePath = "";
                statusMessage.Status = ExtractedFileStatus.ErrorWontRetry;
                routingKey = _options.RoutingKeyFailure;
            }

            _statusMessageProducer.SendMessage(statusMessage, header, routingKey);

            Ack(header, tag);
        }
    }
}
