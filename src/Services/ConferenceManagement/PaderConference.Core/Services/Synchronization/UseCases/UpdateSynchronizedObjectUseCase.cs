﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PaderConference.Core.Services.Synchronization.Gateways;
using PaderConference.Core.Services.Synchronization.Notifications;
using PaderConference.Core.Services.Synchronization.Requests;
using PaderConference.Core.Services.Synchronization.Utilities;

namespace PaderConference.Core.Services.Synchronization.UseCases
{
    public class UpdateSynchronizedObjectUseCase : IRequestHandler<UpdateSynchronizedObjectRequest>
    {
        private readonly IEnumerable<ISynchronizedObjectProvider> _providers;
        private readonly ISynchronizedObjectRepository _synchronizedObjectRepository;
        private readonly ISynchronizedObjectSubscriptionsRepository _subscriptionsRepository;
        private readonly IMediator _mediator;

        public UpdateSynchronizedObjectUseCase(IEnumerable<ISynchronizedObjectProvider> providers,
            ISynchronizedObjectRepository synchronizedObjectRepository,
            ISynchronizedObjectSubscriptionsRepository subscriptionsRepository, IMediator mediator)
        {
            _providers = providers;
            _synchronizedObjectRepository = synchronizedObjectRepository;
            _subscriptionsRepository = subscriptionsRepository;
            _mediator = mediator;
        }

        public async Task<Unit> Handle(UpdateSynchronizedObjectRequest request, CancellationToken cancellationToken)
        {
            var (conferenceId, synchronizedObjectId) = request;

            var provider = GetProvider(synchronizedObjectId);
            var participantIds = await GetParticipantIdsSubscribedTo(conferenceId, synchronizedObjectId);

            if (!participantIds.Any()) return Unit.Value;

            var newValue = await provider.FetchValue(conferenceId, synchronizedObjectId);
            var previousValue =
                await _synchronizedObjectRepository.Create(conferenceId, synchronizedObjectId.ToString(), newValue);

            await _mediator.Publish(
                new SynchronizedObjectUpdatedNotification(conferenceId, participantIds, synchronizedObjectId.ToString(),
                    newValue, previousValue), cancellationToken);

            return Unit.Value;
        }

        private ISynchronizedObjectProvider GetProvider(SynchronizedObjectId synchronizedObjectId)
        {
            var provider = _providers.FirstOrDefault(x => x.Id == synchronizedObjectId.Id);
            if (provider == null)
                throw new InvalidOperationException($"There was no provider registered for {synchronizedObjectId.Id}.");

            return provider;
        }

        private async ValueTask<IImmutableList<string>> GetParticipantIdsSubscribedTo(string conferenceId,
            SynchronizedObjectId synchronizedObjectId)
        {
            var subscriptions = await _subscriptionsRepository.GetOfConference(conferenceId);
            return ConferenceSubscriptionHelper.GetParticipantIdsSubscribedTo(subscriptions, synchronizedObjectId)
                .ToImmutableList();
        }
    }
}