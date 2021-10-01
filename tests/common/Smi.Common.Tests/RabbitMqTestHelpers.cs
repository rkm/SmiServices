using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NUnit.Framework;
using Smi.Common.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Smi.Common.Tests
{
    /// <summary>
    /// Helper class for managing RabbitMQ virtual hosts in tests
    /// </summary>
    public static class RabbitMqTestHelpers
    {
        private const int RABBITMQ_MANAGEMENT_PORT = 15672;
        private const string TEST_VHOST_PREFIX = "smiservices-test-";

        private static readonly HttpClient _client = new();

        private static readonly ILogger _logger;


        static RabbitMqTestHelpers()
        {
            TestLogger.Setup();
            _logger = LogManager.GetLogger(typeof(RabbitMqTestHelpers).Name);

            SetRabbitMqAuth("guest", "guest");
        }

        public static void SetRabbitMqAuth(RabbitOptions o) => SetRabbitMqAuth(o.RabbitMqUserName, o.RabbitMqPassword);

        public static void SetRabbitMqAuth(string username, string password)
        {
            var byteArray = new UTF8Encoding().GetBytes($"{username}:{password}");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        public static string CreateRandomVhost(string rabbitMqHostName)
        {
            var vhostName = $"{TEST_VHOST_PREFIX}{Guid.NewGuid().ToString().Split("-")[^1]}";

            var response = _client.PutAsync($"http://{rabbitMqHostName}:{RABBITMQ_MANAGEMENT_PORT}/api/vhosts/{vhostName}", null).Result;
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, "Expected a new vhost");

            _logger.Debug($"Created vhost '{vhostName}'");
            return vhostName;
        }

        public static IEnumerable<string> ListTestVhosts(string rabbitMqHostName)
        {
            var response = _client.GetAsync($"http://{rabbitMqHostName}:{RABBITMQ_MANAGEMENT_PORT}/api/vhosts/").Result;
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Expected a list of all vhosts");

            using var streamReader = new StreamReader(response.Content.ReadAsStream());
            var vhostList = JsonConvert.DeserializeObject<JArray>(streamReader.ReadToEnd());
            return vhostList
                .Select(v => (string)v["name"])
                .Where(x => x.StartsWith(TEST_VHOST_PREFIX));
        }

        public static void DeleteVhost(string rabbitMqHostName, string vhostName)
        {
            var response = _client.DeleteAsync($"http://{rabbitMqHostName}:{RABBITMQ_MANAGEMENT_PORT}/api/vhosts/{vhostName}").Result;
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode, "Expected to delete a vhost");

            _logger.Debug($"Deleted vhost '{vhostName}'");
        }

        public static void DeleteAllTestVhosts(string rabbitMqHostName)
        {
            _logger.Debug("Deleting all test vhosts...");

            foreach (var vhostName in ListTestVhosts(rabbitMqHostName))
                if (vhostName.StartsWith(TEST_VHOST_PREFIX))
                    DeleteVhost(rabbitMqHostName, vhostName);
        }
    }
}