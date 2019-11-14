﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jasper.Configuration;
using Jasper.Messaging;
using Jasper.Messaging.Durability;
using Jasper.Messaging.Logging;
using Jasper.Messaging.Model;
using Jasper.Messaging.Runtime;
using Jasper.Messaging.Runtime.Invocation;
using Jasper.Messaging.Runtime.Routing;
using Jasper.Messaging.Runtime.Serializers;
using Jasper.Messaging.Scheduled;
using Jasper.Messaging.Transports;
using Jasper.Messaging.Transports.Sending;
using Jasper.Messaging.WorkerQueues;
using Lamar;
using LamarCodeGeneration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jasper.Testing.Messaging
{
    public class MockMessagingRoot : IMessagingRoot
    {
        private ListeningStatus _listeningStatus = ListeningStatus.Accepting;
        public IScheduledJobProcessor ScheduledJobs { get; } = Substitute.For<IScheduledJobProcessor>();
        public IMessageRouter Router { get; } = Substitute.For<IMessageRouter>();
        public IHandlerPipeline Pipeline { get; } = Substitute.For<IHandlerPipeline>();
        public IMessageLogger Logger { get; } = new MessageLogger(new LoggerFactory(), new NulloMetrics());
        public MessagingSerializationGraph Serialization { get; } = MessagingSerializationGraph.Basic();
        public JasperOptions Options { get; } = new JasperOptions();

        public ITransport[] Transports { get; } =
            {Substitute.For<ITransport>(), Substitute.For<ITransport>(), Substitute.For<ITransport>()};

        public IMessageContext NewContext()
        {
            return new MessageContext(this);
        }

        public AdvancedSettings Settings { get; } = new AdvancedSettings();


        public void AddListener(ListenerSettings listenerSettings, IListener agent)
        {

        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public IListeningWorkerQueue BuildDurableListener(IListener agent)
        {
            throw new NotImplementedException();
        }

        public IMessageContext ContextFor(Envelope envelope)
        {
            return new MessageContext(this, envelope);
        }

        public IEnvelopePersistence Persistence { get; } = Substitute.For<IEnvelopePersistence>();
        public ITransportLogger TransportLogger { get; } = Substitute.For<ITransportLogger>();

        public HandlerGraph Handlers { get; } = new HandlerGraph();

        public readonly Dictionary<Uri, ISubscriber> Subscribers = new Dictionary<Uri,ISubscriber>();

        public ISubscriber GetOrBuild(Uri address)
        {
            if (Subscribers.TryGetValue(address, out var subscriber))
            {
                return subscriber;
            }

            return null;
        }

        public ISubscriber[] AllKnown()
        {
            return Subscribers.Values.ToArray();
        }

        public void AddSubscriber(ISubscriber subscriber)
        {
            throw new NotImplementedException();
        }
    }
}
