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
                throw new Exception($"Filesystem root does not exist ('{fileSystemRoot}')");

            if (!_fileSystem.Directory.Exists(extractRoot))
                throw new Exception($"Extract root does not exist ('{extractRoot}')");
        }

        protected override void ProcessMessageImpl(IMessageHeader header, ExtractFileMessage message, ulong tag)
        {
            if (message.IsIdentifiableExtraction)
                throw new Exception("Received a message with IsIdentifiableExtraction set");

            var statusMessage = new ExtractedFileStatusMessage(message);

            var sourceFile = _fileSystem.FileInfo.FromFileName(_fileSystem.Path.Combine(_fileSystemRoot, message.DicomFilePath));

            if (!sourceFile.Exists)
            {
                statusMessage.StatusMessage = $"Could not find file to anonymise ('{message.DicomFilePath}')";
                statusMessage.OutputFilePath = "";
                statusMessage.Status = ExtractedFileStatus.FileMissing;

                _statusMessageProducer.SendMessage(statusMessage, header, _options.RoutingKeyFailure);
                Ack(header, tag);
                return;
            }

            var destFile = _fileSystem.FileInfo.FromFileName(_fileSystem.Path.Combine(_extractRoot, message.ExtractionDirectory, message.OutputPath));

            if (!destFile.Directory.Exists)
            {
                _logger.Debug($"Creating output directory '{destFile.Directory}'");
                destFile.Directory.Create();
            }

            if (_options.FailIfSourceWriteable)
            {
                using var fs = _fileSystem.FileStream.Create(sourceFile.FullName, FileMode.OpenOrCreate, FileAccess.Write);
                if (fs.CanWrite)
                    throw new Exception($"Source file was writeable and FailIfSourceWriteable is set ('{message.DicomFilePath}')");
            }

            string routingKey;

            try
            {
                _anonymiser.Anonymise(sourceFile, destFile);

                statusMessage.StatusMessage = "";
                statusMessage.OutputFilePath = message.OutputPath;
                statusMessage.Status = ExtractedFileStatus.Anonymised;
                routingKey = _options.RoutingKeySuccess;
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Error anonymising '{sourceFile}'");

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
