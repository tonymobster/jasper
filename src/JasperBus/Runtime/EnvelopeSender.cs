using System;
using System.Collections.Generic;
using System.Linq;
using JasperBus.Configuration;
using JasperBus.Runtime.Serializers;

namespace JasperBus.Runtime
{
    public class EnvelopeSender : IEnvelopeSender
    {
        private readonly ChannelGraph _channels;
        private readonly IEnvelopeSerializer _serializer;

        public EnvelopeSender(ChannelGraph channels, IEnvelopeSerializer serializer)
        {
            _channels = channels;
            _serializer = serializer;
        }

        public string Send(Envelope envelope)
        {
            var channels = DetermineDestinationChannels(envelope).ToArray();
            if (!channels.Any())
            {
                throw new Exception($"No channels match this message ({envelope})");
            }

            foreach (var channel in channels)
            {
                _channels.Send(envelope, channel, _serializer);
            }

            return envelope.CorrelationId;
        }

        public IEnumerable<Uri> DetermineDestinationChannels(Envelope envelope)
        {
            var destination = envelope.Destination;

            if (destination != null)
            {
                yield return destination;

                yield break;
            }

            if (envelope.Message != null)
            {
                var messageType = envelope.Message.GetType();
                foreach (var channel in _channels)
                {
                    // TODO -- maybe memoize this one later
                    if (channel.ShouldSendMessage(messageType))
                    {
                        // TODO -- hang on here, should this be the "corrected" Uri
                        yield return channel.Uri;
                    }

                }
            }
        }

        public string Send(Envelope envelope, IMessageCallback callback)
        {
            throw new System.NotImplementedException();
        }

        public void SendOutgoingMessages(Envelope original, IEnumerable<object> cascadingMessages)
        {
            throw new System.NotImplementedException();
        }

        public void SendFailureAcknowledgement(Envelope original, string message)
        {
            throw new System.NotImplementedException();
        }
    }
}