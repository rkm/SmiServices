using NUnit.Framework;
using Smi.Common.Options;
using System.Linq;

namespace Smi.Common.Tests
{
    [RequiresRabbit]
    public class RabbitMqTestHelpersTests
    {
        private RabbitOptions _rabbitOptions;

        #region Fixture Methods

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            TestLogger.Setup();

            _rabbitOptions =
               new GlobalOptionsFactory()
               .Load(nameof(RabbitMqTestHelpersTests))
               .RabbitOptions;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() { }

        #endregion

        #region Test Methods

        [SetUp]
        public void SetUp()
        {
            RabbitMqTestHelpers.DeleteAllTestVhosts(_rabbitOptions.RabbitMqHostName);
        }

        [TearDown]
        public void TearDown() { }

        #endregion

        #region Tests

        [Test]
        public void SetRabbitMqAuth_InvalidCredentials_Fails()
        {
            RabbitMqTestHelpers.SetRabbitMqAuth("foo", "bar");

            Assert.Throws<AssertionException>(
                () => RabbitMqTestHelpers.CreateRandomVhost(_rabbitOptions.RabbitMqHostName)
            );
        }

        [Test]
        public void CreateRandomVhost_HappyPath()
        {
            var vhostName = RabbitMqTestHelpers.CreateRandomVhost(_rabbitOptions.RabbitMqHostName);
            _rabbitOptions.RabbitMqVirtualHost = vhostName;

            // Creates a test connection on construction
            var _ = new RabbitMqAdapter(_rabbitOptions.CreateConnectionFactory(), "TestHost");
        }

        [Test]
        public void ListTestVhosts_IncludesNew()
        {
            var before = RabbitMqTestHelpers.ListTestVhosts(_rabbitOptions.RabbitMqHostName).ToList();

            var vhostName = RabbitMqTestHelpers.CreateRandomVhost(_rabbitOptions.RabbitMqHostName);

            var after = RabbitMqTestHelpers.ListTestVhosts(_rabbitOptions.RabbitMqHostName);

            before.Add(vhostName);
            Assert.True(before.OrderBy(x => x).SequenceEqual(after.OrderBy(x => x)));
        }

        [Test]
        public void DeleteVhost_SpecificVhost()
        {
            var vhost1 = RabbitMqTestHelpers.CreateRandomVhost(_rabbitOptions.RabbitMqHostName);
            var vhost2 = RabbitMqTestHelpers.CreateRandomVhost(_rabbitOptions.RabbitMqHostName);

            RabbitMqTestHelpers.DeleteVhost(_rabbitOptions.RabbitMqHostName, vhost1);

            var allVhosts = RabbitMqTestHelpers.ListTestVhosts(_rabbitOptions.RabbitMqHostName).ToList();
            Assert.False(allVhosts.Contains(vhost1));
            Assert.True(allVhosts.Contains(vhost2));
        }

        [Test]
        public void DeleteAllTestVhosts_DeletesEverything()
        {
            RabbitMqTestHelpers.CreateRandomVhost(_rabbitOptions.RabbitMqHostName);
            RabbitMqTestHelpers.CreateRandomVhost(_rabbitOptions.RabbitMqHostName);

            RabbitMqTestHelpers.DeleteAllTestVhosts(_rabbitOptions.RabbitMqHostName);

            Assert.IsEmpty(RabbitMqTestHelpers.ListTestVhosts(_rabbitOptions.RabbitMqHostName));
        }

        #endregion
    }
}
