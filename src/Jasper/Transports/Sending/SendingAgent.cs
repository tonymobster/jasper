using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Baseline;
using Jasper.Configuration;
using Jasper.Logging;
using Jasper.Transports.Tcp;

namespace Jasper.Transports.Sending
{
    public abstract class SendingAgent : ISendingAgent, ISenderCallback, ICircuit
    {
        private readonly ITransportLogger _logger;
        private readonly IMessageLogger _messageLogger;
        protected readonly ISender _sender;
        protected readonly AdvancedSettings _settings;
        private int _failureCount;
        private CircuitWatcher _circuitWatcher;

        public SendingAgent(ITransportLogger logger, IMessageLogger messageLogger, ISender sender, AdvancedSettings settings, Endpoint endpoint)
        {
            _logger = logger;
            _messageLogger = messageLogger;
            _sender = sender;
            _settings = settings;
            Endpoint = endpoint;
            _sending = new ActionBlock<Envelope>(sendViaSender, Endpoint.ExecutionOptions);

            _sending.Completion.ContinueWith(t =>
            {
                Console.WriteLine(t.Exception?.ToString());
            });
        }

        public Endpoint Endpoint { get; }

        public Uri ReplyUri { get; set; }

        public Uri Destination => _sender.Destination;

        public void Dispose()
        {
            _sender.Dispose();
        }

        public bool Latched { get; private set; }
        public abstract bool IsDurable { get; }

        private void setDefaults(Envelope envelope)
        {
            envelope.Status = EnvelopeStatus.Outgoing;
            envelope.OwnerId = _settings.UniqueNodeId;
            envelope.ReplyUri = envelope.ReplyUri ?? ReplyUri;
        }

        public async Task EnqueueOutgoing(Envelope envelope)
        {
            setDefaults(envelope);
           _sending.Post(envelope);
           _messageLogger.Sent(envelope);
        }

        public async Task Forward(Envelope envelope)
        {
            setDefaults(envelope);

            await storeAndForward(envelope);

            _messageLogger.Sent(envelope);
        }

        protected abstract Task storeAndForward(Envelope envelope);

        public Task<bool> TryToResume(CancellationToken cancellationToken)
        {
            return _sender.Ping(cancellationToken);
        }
        TimeSpan ICircuit.RetryInterval => Endpoint.PingIntervalForCircuitResume;

        Task ICircuit.Resume(CancellationToken cancellationToken)
        {
            _circuitWatcher = null;

            Unlatch();

            return afterRestarting(_sender);
        }

        protected abstract Task afterRestarting(ISender sender);
        
        public abstract Task Successful(Envelope outgoing);

        private ActionBlock<Envelope> _sending;
        public Task LatchAndDrain()
        {
            Latched = true;

            _sending.Complete();

            _logger.CircuitBroken(Destination);

            return Task.CompletedTask;
        }

        public void Unlatch()
        {
            _logger.CircuitResumed(Destination);

            Latched = false;
        }

        private async Task sendViaSender(Envelope envelope)
        {
            try
            {
                await _sender.Send(envelope);

                await Successful(envelope);
            }
            catch (Exception e)
            {
                try
                {
                    await ((ISenderCallback)this).ProcessingFailure(envelope, e);
                }
                catch (Exception exception)
                {
                    _logger.LogException(exception);
                }
            }
        }

        public async Task MarkFailed(OutgoingMessageBatch batch)
        {
            // If it's already latched, just enqueue again
            if (Latched)
            {
                await EnqueueForRetry(batch);
                return;
            }

            _failureCount++;

            if (_failureCount >= Endpoint.FailuresBeforeCircuitBreaks)
            {
                await LatchAndDrain();
                await EnqueueForRetry(batch);

                _circuitWatcher = new CircuitWatcher(this, _settings.Cancellation);
            }
            else
            {
                foreach (var envelope in batch.Messages)
                {
#pragma warning disable 4014
                    _sender.Send(envelope);
#pragma warning restore 4014
                }
            }
        }


        public abstract Task EnqueueForRetry(OutgoingMessageBatch batch);


        public Task MarkSuccess()
        {
            _failureCount = 0;
            Unlatch();
            _circuitWatcher = null;

            return Task.CompletedTask;
        }
        

        Task ISenderCallback.TimedOut(OutgoingMessageBatch outgoing)
        {
            _logger.OutgoingBatchFailed(outgoing);
            return MarkFailed(outgoing);
        }

        Task ISenderCallback.SerializationFailure(OutgoingMessageBatch outgoing)
        {
            _logger.OutgoingBatchFailed(outgoing);
            // Can't really happen now, but what the heck.
            _logger.LogException(new Exception("Serialization failure with outgoing envelopes " +
                                               outgoing.Messages.Select(x => x.ToString()).Join(", ")));

            return Task.CompletedTask;
        }

        Task ISenderCallback.QueueDoesNotExist(OutgoingMessageBatch outgoing)
        {
            _logger.OutgoingBatchFailed(outgoing, new QueueDoesNotExistException(outgoing));

            return Task.CompletedTask;
        }

        Task ISenderCallback.ProcessingFailure(OutgoingMessageBatch outgoing)
        {
            _logger.OutgoingBatchFailed(outgoing);
            return MarkFailed(outgoing);
        }

        Task ISenderCallback.ProcessingFailure(Envelope outgoing, Exception exception)
        {
            var batch = new OutgoingMessageBatch(outgoing.Destination, new[] { outgoing });
            _logger.OutgoingBatchFailed(batch, exception);
            return MarkFailed(batch);
        }

        Task ISenderCallback.ProcessingFailure(OutgoingMessageBatch outgoing, Exception exception)
        {
            _logger.LogException(exception,
                message: $"Failure trying to send a message batch to {outgoing.Destination}");
            _logger.OutgoingBatchFailed(outgoing, exception);
            return MarkFailed(outgoing);
        }

        Task ISenderCallback.SenderIsLatched(OutgoingMessageBatch outgoing)
        {
            return MarkFailed(outgoing);
        }

        public abstract Task Successful(OutgoingMessageBatch outgoing);

        public bool SupportsNativeScheduledSend => _sender.SupportsNativeScheduledSend;

    }
}
