import { SendChatMessageDto, CreateRoomDto, SwitchRoomDto } from './core-hub.types';
import { connectSignal, invoke, send } from './store/signal/actions';

export const joinConference = (conferenceId: string, defaultEvents: string[], accessToken: string) =>
   connectSignal({ conferenceId, access_token: accessToken }, defaultEvents, { conferenceId });

export const joinConferenceAsEquipment = (conferenceId: string, defaultEvents: string[], token: string) =>
   connectSignal({ conferenceId, token }, defaultEvents, { conferenceId });

export const openConference = () => send('OpenConference');
export const closeConference = () => send('CloseConference');

export const createRooms = (rooms: CreateRoomDto[]) => send('CreateRooms', rooms);
export const removeRooms = (roomIds: string[]) => send('RemoveRooms', roomIds);
export const switchRoom = (dto: SwitchRoomDto) => send('SwitchRoom', dto);

export const loadFullChat = () => send('RequestChat');

export const sendChatMessage = (dto: SendChatMessageDto) => send('SendChatMessage', dto);

export const _getEquipmentToken = 'GetEquipmentToken';
export const getEquipmentToken = () => invoke(_getEquipmentToken);