namespace Strive.Core.Services.Permissions
{
    public static class DefinedPermissions
    {
        public static class Conference
        {
            public static readonly PermissionDescriptor<bool> CanOpenAndClose = new("conference/canOpenAndClose");
            public static readonly PermissionDescriptor<bool> CanKickParticipant = new("conference/canKickParticipant");
        }

        public static class Permissions
        {
            public static readonly PermissionDescriptor<bool> CanGiveTemporaryPermission =
                new("permissions/canGiveTemporaryPermission");

            public static readonly PermissionDescriptor<bool> CanSeeAnyParticipantsPermissions =
                new("permissions/canSeeAnyParticipantsPermissions");
        }

        public static class Chat
        {
            public static readonly PermissionDescriptor<bool> CanSendChatMessage = new("chat/canSendMessage");
            public static readonly PermissionDescriptor<bool> CanSendAnnouncement = new("chat/canSendAnnouncement");
            public static readonly PermissionDescriptor<bool> CanSendAnonymously = new("chat/canSendAnonymously");
        }

        public static class Media
        {
            public static readonly PermissionDescriptor<bool> CanShareAudio = new("media/canShareAudio");
            public static readonly PermissionDescriptor<bool> CanShareScreen = new("media/canShareScreen");
            public static readonly PermissionDescriptor<bool> CanShareWebcam = new("media/canShareWebcam");

            public static readonly PermissionDescriptor<bool> CanChangeOtherParticipantsProducers =
                new("media/canChangeOtherParticipantsProducers");
        }

        public static class Rooms
        {
            public static readonly PermissionDescriptor<bool> CanCreateAndRemove = new("rooms/canCreateAndRemove");
            public static readonly PermissionDescriptor<bool> CanSwitchRoom = new("rooms/canSwitchRoom");
        }

        public static class Scenes
        {
            public static readonly PermissionDescriptor<bool> CanSetScene = new("scenes/canSetScene");

            public static readonly PermissionDescriptor<bool> CanOverwriteContentScene =
                new("scenes/canOverwriteContentScene");

            public static readonly PermissionDescriptor<bool> CanPassTalkingStick = new("scenes/talkingStick_canPass");
            public static readonly PermissionDescriptor<bool> CanTakeTalkingStick = new("scenes/talkingStick_canTake");

            public static readonly PermissionDescriptor<bool> CanQueueForTalkingStick =
                new("scenes/talkingStick_canQueue");
        }

        public static class Poll
        {
            public static readonly PermissionDescriptor<bool> CanOpenPoll = new("poll/canOpen");

            public static readonly PermissionDescriptor<bool> CanSeeUnpublishedPollResults =
                new("poll/canSeeUnpublishedPollResults");
        }
    }
}
