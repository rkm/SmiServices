﻿
using Moq;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using Smi.Common.Messages;
using Smi.Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Smi.Common.Tests
{
    [TestFixture]
    public class ConsumerTests
    {
        [Test]
        public void Consumer_UnhandledException_TriggersFatal()
        {
            var mockDeliverArgs = Mock.Of<BasicDeliverEventArgs>();
            mockDeliverArgs.DeliveryTag = 1;

            var consumer = new TestConsumer();
            consumer.SetModel(Mock.Of<IModel>());

            var fatalCalled = false;
            consumer.OnFatal += (sender, args) => fatalCalled = true;

            consumer.ProcessMessage(mockDeliverArgs);

            Thread.Sleep(1000);
            Assert.True(fatalCalled);
        }

    }

    public class TestConsumer : Consumer
    {
        protected override void ProcessMessageImpl(IMessageHeader header, BasicDeliverEventArgs basicDeliverEventArgs)
        {
            throw new Exception("Throwing to trigger Fatal");
        }
    }

    public class DoNothingConsumer : Consumer
    {
        protected override void ProcessMessageImpl(IMessageHeader header, BasicDeliverEventArgs basicDeliverEventArgs)
        {
            
        }
    }

    public class SelfClosingConsumer : Consumer
    {
        protected override void ProcessMessageImpl(IMessageHeader header, BasicDeliverEventArgs basicDeliverEventArgs)
        {
            Model.Close();
        }
    }

}
