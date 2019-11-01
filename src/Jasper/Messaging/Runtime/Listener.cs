using System;
using System.Linq;
using System.Threading.Tasks;
using Jasper.Messaging.Durability;
using Jasper.Messaging.Logging;
using Jasper.Messaging.Transports;
using Jasper.Messaging.Transports.Tcp;
using Jasper.Messaging.WorkerQueues;

namespace Jasper.Messaging.Runtime
{
    public class Listener : IListener
    {
        private readonly IListeningAgent _agent;
        private readonly ITransportLogger _logger;
        private readonly IEnvelopePersistence _persistence;
        private readonly IWorkerQueue _queue;
        private readonly JasperOptions _options;

        public Listener(IListeningAgent agent, IWorkerQueue queue, ITransportLogger logger,
            JasperOptions options, IEnvelopePersistence persistence)
        {
            _agent = agent;
            _queue = queue;
            _logger = logger;
            _options = options;
            _persistence = persistence;
        }

        public Uri Address => _agent.Address;

        public ListeningStatus Status
        {
            get => _agent.Status;
            set => _agent.Status = value;
        }

        public async Task<ReceivedStatus> Received(Uri uri, Envelope[] messages)
        {
            var now = DateTime.UtcNow;

            return await ProcessReceivedMessages(now, uri, messages);
        }

        public Task Acknowledged(Envelope[] messages)
        {
            return Task.CompletedTask;
        }

        public Task NotAcknowledged(Envelope[] messages)
        {
            return _persistence.DeleteIncomingEnvelopes(messages);
        }

        public Task Failed(Exception exception, Envelope[] messages)
        {
            _logger.LogException(new MessageFailureException(messages, exception));
            return Task.CompletedTask;
        }


        public void Dispose()
        {
            // nothing
        }

        public void Start()
        {
            _agent.Start(this);
        }

        // Separated for testing here.
        public async Task<ReceivedStatus> ProcessReceivedMessages(DateTime now, Uri uri, Envelope[] envelopes)
        {
            if (_options.Cancellation.IsCancellationRequested) return ReceivedStatus.ProcessFailure;

            try
            {
                foreach (var envelope in envelopes)
                {
                    envelope.ReceivedAt = uri;

                    if (envelope.IsDelayed(now))
                    {
                        envelope.Status = TransportConstants.Scheduled;
                        envelope.OwnerId = TransportConstants.AnyNode;
                        await _queue.ScheduleExecution(envelope);
                    }
                    else
                    {
                        envelope.Status = TransportConstants.Incoming;
                        envelope.OwnerId = _options.UniqueNodeId;
                    }
                }

                await _persistence.StoreIncoming(envelopes);


                foreach (var message in envelopes.Where(x => x.Status == TransportConstants.Incoming))
                {
                    await _queue.Enqueue(message);
                }

                _logger.IncomingBatchReceived(envelopes);

                return ReceivedStatus.Successful;
            }
            catch (Exception e)
            {
                _logger.LogException(e);
                return ReceivedStatus.ProcessFailure;
            }
        }
    }
}
