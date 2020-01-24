﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microservices.IsIdentifiable.Service;
using NUnit.Framework;
using Smi.Common.Messages;
using Smi.Common.Messages.Extraction;
using Smi.Common.Options;
using Smi.Common.Tests;

namespace Microservices.IsIdentifiable.Tests.ServiceTests
{
    [TestFixture, RequiresRabbit]
    class IsIdentifiableHostTests
    {[Test]
        public void TestClassifierName_NoClassifier()
        {
            var options = GlobalOptions.Load("default.yaml", TestContext.CurrentContext.TestDirectory);
            
            options.IsIdentifiableOptions.ClassifierType = "";
            var ex = Assert.Throws<ArgumentException>(()=>new IsIdentifiableHost(options, false));
            StringAssert.Contains("No IClassifier has been set in options.  Enter a value for " + nameof(options.IsIdentifiableOptions.ClassifierType),ex.Message);
        }
        [Test]
        public void TestClassifierName_NotRecognized()
        {
            var options = GlobalOptions.Load("default.yaml", TestContext.CurrentContext.TestDirectory);
            options.IsIdentifiableOptions.DataDirectory = TestContext.CurrentContext.WorkDirectory;

            options.IsIdentifiableOptions.ClassifierType = "HappyFunTimes";
            var ex = Assert.Throws<TypeLoadException>(()=>new IsIdentifiableHost(options, false));
            StringAssert.Contains("Could not load type 'HappyFunTimes' from",ex.Message);
        }

        [Test]
        public void TestClassifierName_ValidClassifier()
        {
            var options = GlobalOptions.Load("default.yaml", TestContext.CurrentContext.TestDirectory);

            var testDcm = new FileInfo(Path.Combine(TestContext.CurrentContext.TestDirectory, nameof(TestClassifierName_ValidClassifier),"f1.dcm"));Path.Combine(TestContext.CurrentContext.TestDirectory, nameof(TestClassifierName_ValidClassifier),"f1.dcm");
            TestData.Create(testDcm);

            using(var tester = new MicroserviceTester(options.RabbitOptions, options.IsIdentifiableOptions))
            {
                options.IsIdentifiableOptions.ClassifierType = typeof(RejectAllClassifier).FullName;
                options.IsIdentifiableOptions.DataDirectory = TestContext.CurrentContext.TestDirectory;

                var host = new IsIdentifiableHost(options, false);
                Assert.IsNotNull(host);
                host.Start();

                tester.SendMessage(options.IsIdentifiableOptions ,new ExtractFileMessage(new ExtractionRequestMessage())
                {
                    DicomFilePath = testDcm.FullName,
                    OutputPath = TestContext.CurrentContext.WorkDirectory,
                    ProjectNumber = "100",
                    ExtractionDirectory = "./fish",
                });

                var awaiter = new TestTimelineAwaiter();
                awaiter.Await(()=>host.Consumer.AckCount == 1);
            }
        }
    }
}
