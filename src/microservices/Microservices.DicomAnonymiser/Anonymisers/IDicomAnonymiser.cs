using Smi.Common.Messages.Extraction;
using System.IO.Abstractions;

namespace Microservices.DicomAnonymiser.Anonymisers
{
    public interface IDicomAnonymiser
    {
        ExtractedFileStatus Anonymise(IFileInfo sourceFile, IFileInfo destFile);
    }
}
