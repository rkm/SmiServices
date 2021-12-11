using Smi.Common.Options;

namespace Smi.Common.Tests.Options
{
    public class FileSystemOptionsTests
    {
        [Test]
        public void TestFileSystemOptions_AsLinuxRootDir()
        {

            var opts = new FileSystemOptions()
            {
                FileSystemRoot = "/",
                ExtractRoot = "/",
            };

            Assert.AreEqual("/", opts.FileSystemRoot);
            Assert.AreEqual("/", opts.ExtractRoot);
        }
    }
}