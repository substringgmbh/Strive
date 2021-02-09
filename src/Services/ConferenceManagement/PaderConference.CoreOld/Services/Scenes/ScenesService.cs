﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using PaderConference.Core.Extensions;
using PaderConference.Core.Interfaces;
using PaderConference.Core.NewServices.Permissions;
using PaderConference.Core.Services.Rooms;
using PaderConference.Core.Services.Scenes.Requests;
using PaderConference.Core.Services.Synchronization;

namespace PaderConference.Core.Services.Scenes
{
    public class ScenesService : ConferenceService
    {
        private readonly IRoomManagement _roomManagement;
        private readonly IPermissionsService _permissionsService;
        private readonly ILogger<ScenesService> _logger;
        private readonly ScenesOptions _options;
        private readonly ISynchronizedObject<IImmutableDictionary<string, RoomSceneState>> _synchronizedObject;
        private readonly AsyncLock _roomManagementLock = new AsyncLock();

        public ScenesService(IRoomManagement roomManagement, ISynchronizationManager synchronizationManager,
            IOptions<ScenesOptions> options, IPermissionsService permissionsService, ILogger<ScenesService> logger)
        {
            _roomManagement = roomManagement;
            _permissionsService = permissionsService;
            _logger = logger;
            _options = options.Value;
            _synchronizedObject =
                synchronizationManager.Register<IImmutableDictionary<string, RoomSceneState>>("scenes",
                    ImmutableDictionary<string, RoomSceneState>.Empty);
        }

        public override async ValueTask InitializeAsync()
        {
            _roomManagement.RoomsCreated += OnRoomsCreated;
            _roomManagement.RoomsRemoved += OnRoomsRemoved;

            using (await _roomManagementLock.LockAsync())
            {
                await _synchronizedObject.Update(
                    _roomManagement.State.Rooms.ToImmutableDictionary(x => x.RoomId, CreateDefaultState));
            }
        }

        public override ValueTask DisposeAsync()
        {
            _roomManagement.RoomsCreated -= OnRoomsCreated;
            _roomManagement.RoomsRemoved -= OnRoomsRemoved;

            return base.DisposeAsync();
        }

        public async ValueTask<SuccessOrError> SetScene(IServiceMessage<ChangeSceneRequest> message)
        {
            var permissions = await _permissionsService.GetPermissions(message.Participant);
            if (!await permissions.GetPermissionValue(DefinedPermissions.Scenes.CanSetScene))
                return CommonError.PermissionDenied(DefinedPermissions.Scenes.CanSetScene);

            using (await _roomManagementLock.LockAsync())
            {
                if (!_synchronizedObject.Current.ContainsKey(message.Payload.RoomId))
                    return SceneError.RoomNotFound;

                await _synchronizedObject.Update(current => current.SetItem(message.Payload.RoomId,
                    message.Payload.Scene));
            }

            return SuccessOrError.Succeeded;
        }

        private async void OnRoomsRemoved(object? sender, IReadOnlyList<string> e)
        {
            using var _ = _logger.BeginMethodScope();

            _logger.LogDebug("Remove rooms {@rooms}, update synchronized object", e);
            using (await _roomManagementLock.LockAsync())
            {
                await _synchronizedObject.Update(current => current.RemoveRange(e));
            }
        }

        private async void OnRoomsCreated(object? sender, IReadOnlyList<Room> e)
        {
            using var _ = _logger.BeginMethodScope();

            _logger.LogDebug("Create {x} room(s), update synchronized object", e.Count);
            using (await _roomManagementLock.LockAsync())
            {
                await _synchronizedObject.Update(current =>
                    current.AddRange(e.Select(x =>
                        new KeyValuePair<string, RoomSceneState>(x.RoomId, CreateDefaultState(x)))));
            }
        }

        private RoomSceneState CreateDefaultState(Room room)
        {
            if (room.RoomId == _roomManagement.State.DefaultRoomId)
                return _options.DefaultRoomState;

            return _options.RoomState;
        }
    }
}