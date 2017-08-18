﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Baseline;
using Jasper.Bus.Configuration;
using Jasper.Bus.Model;
using Jasper.Conneg;
using StructureMap.Graph.Scanning;

namespace Jasper.Bus.Runtime.Subscriptions.New
{
    public class CapabilityGraph : ISubscriptions, IPublishing
    {
        private readonly IList<PublishedMessage> _published = new List<PublishedMessage>();
        private readonly IList<SubscriptionRequirement> _requirements = new List<SubscriptionRequirement>();
        private readonly IList<Func<Type, bool>> _publishFilters = new List<Func<Type, bool>>();

        public CapabilityGraph()
        {
            _publishFilters.Add(type => type.HasAttribute<PublishAttribute>());
        }

        public Uri DefaultReceiverLocation { get; set; }

        public void Publish(Type messageType)
        {
            _published.Add(new PublishedMessage(messageType));
        }

        public Task<ServiceCapabilities> Compile(HandlerGraph handlers, SerializationGraph serialization,
            ChannelGraph channels, JasperRuntime runtime)
        {
            if (runtime.ApplicationAssembly == null)
            {
                return Task.FromResult(compile(handlers, serialization, channels));
            }

            return TypeRepository.FindTypes(runtime.ApplicationAssembly,
                    TypeClassification.Closed | TypeClassification.Closed, type => _publishFilters.Any(x => x(type)))
                .ContinueWith(t =>
                {
                    foreach (var type in t.Result)
                    {
                        Publish(type);
                    }

                    return compile(handlers, serialization, channels);
                });



        }



        private ServiceCapabilities compile(HandlerGraph handlers, SerializationGraph serialization,
            ChannelGraph channels)
        {
            return new ServiceCapabilities
            {
                ServiceName = channels.Name,
                Subscriptions = determineSubscriptions(handlers, serialization, channels),
                Published = determinePublishedMessages(serialization, channels)
            };
        }

        private PublishedMessage[] determinePublishedMessages(SerializationGraph serialization, ChannelGraph channels)
        {
            foreach (var published in _published)
            {
                published.ServiceName = channels.Name;
                var writer = serialization.WriterFor(published.DotNetType);
                published.ContentTypes = writer.ContentTypes;
            }

            return _published.ToArray();
        }

        private NewSubscription[] determineSubscriptions(HandlerGraph handlers, SerializationGraph serialization,
            ChannelGraph channels)
        {
            var messageTypes = handlers.Chains
                .Select(x => x.MessageType)
                .Where(t => t.GetTypeInfo().Assembly != GetType().GetTypeInfo().Assembly)
                .ToArray();

            return _requirements.SelectMany(x =>
                    x.DetermineSubscriptions(serialization, messageTypes, DefaultReceiverLocation))
                .Distinct()
                .Each(x => x.ServiceName = channels.Name)
                .ToArray();
        }

        void ISubscriptionReceiverExpression.At(Uri incoming)
        {
            DefaultReceiverLocation = incoming;
        }

        void ISubscriptionReceiverExpression.At(string incomingUriString)
        {
            DefaultReceiverLocation = incomingUriString.ToUri();
        }

        private ISubscriptionExpression add(Action<ISubscriptionExpression> configure)
        {
            var requirement = new SubscriptionRequirement();
            configure(requirement);
            _requirements.Add(requirement);

            return requirement;
        }

        ISubscriptionExpression ISubscriptionExpression.To<T>()
        {
            return add(x => x.To<T>());
        }

        ISubscriptionExpression ISubscriptionExpression.To(Type messageType)
        {
            return add(x => x.To(messageType));
        }

        ISubscriptionExpression ISubscriptionExpression.To(Func<Type, bool> filter)
        {
            return add(x => x.To(filter));
        }

        ISubscriptionReceiverExpression ISubscriptions.ToAllMessages()
        {
            return add(x => x.To(t => true));
        }

        IPublishing IPublishing.Message<T>()
        {
            Publish(typeof(T));
            return this;
        }

        IPublishing IPublishing.Message(Type messageType)
        {
            Publish(messageType);
            return this;
        }

        IPublishing IPublishing.MessagesMatching(Func<Type, bool> filter)
        {
            _publishFilters.Add(filter);
            return this;
        }
    }
}