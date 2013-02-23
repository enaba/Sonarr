﻿using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Eventing;
using NzbDrone.Test.Common;

namespace NzbDrone.Common.Test.EventingTests
{
    [TestFixture]
    public class ServiceNameFixture : TestBase
    {
        [Test]
        public void should_publish_event_to_handlers()
        {
            var intHandler = new Mock<IHandle<int>>();
            var aggregator = new EventAggregator(TestLogger, new List<IHandle> { intHandler.Object });
            aggregator.Publish(12);

            intHandler.Verify(c => c.Handle(12), Times.Once());
        }

        [Test]
        public void should_publish_to_more_than_one_handler()
        {
            var intHandler1 = new Mock<IHandle<int>>();
            var intHandler2 = new Mock<IHandle<int>>();
            var aggregator = new EventAggregator(TestLogger, new List<IHandle> { intHandler1.Object, intHandler2.Object });
            aggregator.Publish(12);

            intHandler1.Verify(c => c.Handle(12), Times.Once());
            intHandler2.Verify(c => c.Handle(12), Times.Once());
        }

        [Test]
        public void should_not_publish_to_incompatible_handlers()
        {
            var intHandler = new Mock<IHandle<int>>();
            var stringHandler = new Mock<IHandle<string>>();
            var aggregator = new EventAggregator(TestLogger, new List<IHandle> { intHandler.Object, stringHandler.Object });

            aggregator.Publish(12);

            intHandler.Verify(c => c.Handle(12), Times.Once());
            stringHandler.Verify(c => c.Handle(It.IsAny<string>()), Times.Never());
        }

    }

}