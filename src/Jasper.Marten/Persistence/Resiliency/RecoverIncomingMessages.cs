﻿using System.Linq;
using System.Threading.Tasks;
using Jasper.Bus.Logging;
using Jasper.Bus.Transports;
using Jasper.Bus.Transports.Configuration;
using Jasper.Bus.WorkerQueues;
using Marten;

namespace Jasper.Marten.Persistence.Resiliency
{
    public class RecoverIncomingMessages : IMessagingAction
    {
        public static readonly int IncomingMessageLockId = "recover-incoming-messages".GetHashCode();
        private readonly IWorkerQueue _workers;
        private readonly BusSettings _settings;
        private readonly OwnershipMarker _marker;
        private readonly ISchedulingAgent _schedulingAgent;
        private readonly CompositeTransportLogger _logger;

        public RecoverIncomingMessages(IWorkerQueue workers, BusSettings settings, OwnershipMarker marker, ISchedulingAgent schedulingAgent, CompositeTransportLogger logger)
        {
            _workers = workers;
            _settings = settings;
            _marker = marker;
            _schedulingAgent = schedulingAgent;
            _logger = logger;
        }

        public async Task Execute(IDocumentSession session)
        {
            if (!await session.TryGetGlobalTxLock(IncomingMessageLockId))
            {
                return;
            }


            // Have it loop until either a time limit has been reached or a certain
            // queue count has been reached
            // TODO -- how many do you pull in here? Is it configurable? Do you take into account the
            // depth of the worker queue?


            // It's a List behind the covers, but to get R# to shut up, I did the ToArray()
            var incoming = (await session.QueryAsync(new FindAtLargeEnvelopes
            {
                Status = TransportConstants.Incoming
            })).ToArray();

            if (!incoming.Any()) return;

            await _marker.MarkIncomingOwnedByThisNode(session, incoming);

            await session.SaveChangesAsync();

            _logger.RecoveredIncoming(incoming);

            foreach (var envelope in incoming)
            {
                envelope.OwnerId = _settings.UniqueNodeId;
                await _workers.Enqueue(envelope);
            }

            // TODO -- determine if it should schedule another incoming recovery batch immediately
        }


    }
}
