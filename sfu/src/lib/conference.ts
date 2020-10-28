import { Redis } from 'ioredis';
import _ from 'lodash';
import { Router } from 'mediasoup/lib/Router';
import { Consumer, MediaKind, Producer, RtpCapabilities, WebRtcTransportOptions } from 'mediasoup/lib/types';
import config from '../config';
import Connection from './connection';
import Logger from './logger';
import { StreamInfoRepo } from './pader-conference/steam-info-repo';
import { Participant, ProducerSource } from './participant';
import { RoomManager } from './room-manager';
import { ISignalWrapper } from './signal-wrapper';
import {
   ChangeStreamRequest,
   ConnectionMessage,
   ConnectTransportRequest,
   CreateTransportRequest,
   CreateTransportResponse,
   TransportProduceRequest,
   TransportProduceResponse,
} from './types';

const logger = new Logger('Conference');

export class Conference {
   private connections: Map<string, Connection> = new Map();
   private roomManager: RoomManager;

   /** participantId -> Participant */
   private participants: Map<string, Participant> = new Map();
   private streamInfoRepo: StreamInfoRepo;

   constructor(private router: Router, public conferenceId: string, private signal: ISignalWrapper, redis: Redis) {
      this.roomManager = new RoomManager(conferenceId, signal, router, redis);
      this.streamInfoRepo = new StreamInfoRepo(redis, conferenceId);
   }

   get routerCapabilities(): RtpCapabilities {
      return this.router.rtpCapabilities;
   }

   public close(): void {
      logger.info('Close conference %s', this.conferenceId);
      this.router.close();
   }

   public async addConnection(connection: Connection): Promise<void> {
      // create locally
      this.connections.set(connection.connectionId, connection);

      // search participant, check if it already exists
      let participant = this.participants.get(connection.participantId);
      if (!participant) {
         participant = new Participant(connection.participantId);

         // does not exist, add
         this.participants.set(connection.participantId, participant);
      }

      // add connection
      participant.connections.push(connection);

      // notify room manager
      await this.roomManager.updateParticipant(participant);

      // update streams
      await this.streamInfoRepo.updateStreams(this.participants.values());
   }

   public async removeConnection(connectionId: string): Promise<void> {
      const connection = this.connections.get(connectionId);
      if (connection) {
         this.connections.delete(connection.connectionId);

         // if that was the last connection of this participant, remove participant
         const participant = this.participants.get(connection.participantId);
         if (participant) {
            // remove connection from participant
            _.remove(participant.connections, (x) => x.connectionId === connection.connectionId);

            for (const [, producer] of connection.producers) {
               producer.close();
               this.removeProducer(producer, participant);
            }
            await this.roomManager.updateParticipant(participant);

            if (participant.connections.length === 0) {
               // remove participant
               this.participants.delete(participant.participantId);
               await this.roomManager.removeParticipant(participant);
            }
         }

         // update streams
         await this.streamInfoRepo.updateStreams(this.participants.values());
      }
   }

   public async roomSwitched({ meta: { participantId } }: ConnectionMessage<any>): Promise<void> {
      const participant = this.participants.get(participantId);
      if (participant) {
         await this.roomManager.updateParticipant(participant);

         // update streams
         await this.streamInfoRepo.updateStreams(this.participants.values());
      }
   }

   public async changeStream({ payload: { id, type, action }, meta }: ChangeStreamRequest): Promise<void> {
      const connection = this.connections.get(meta.connectionId);
      const participant = this.participants.get(meta.participantId);

      if (connection) {
         let stream: Producer | Consumer | undefined;
         if (type === 'consumer') {
            stream = connection.consumers.get(id);
         } else if (type === 'producer') {
            stream = connection.producers.get(id);

            if (participant && stream) this.removeProducer(stream, participant);
         }

         if (stream) {
            if (action === 'pause') {
               await stream.pause();
            } else if (action === 'close') {
               stream.close();

               if (type === 'consumer') {
                  connection.consumers.delete(id);
               } else {
                  connection.producers.delete(id);
               }
            } else if (action === 'resume') {
               await stream.resume();
            }

            // update streams
            await this.streamInfoRepo.updateStreams(this.participants.values());
         }
      }
   }

   public async transportProduce({
      payload: { transportId, appData, kind, ...producerOptions },
      meta,
   }: TransportProduceRequest): Promise<TransportProduceResponse> {
      const connection = this.connections.get(meta.connectionId);
      if (!connection) throw new Error('Connection was not found');

      const participant = this.participants.get(connection.participantId);
      if (!participant) throw new Error('Participant was not found');

      const transport = connection.transport.get(transportId);
      if (!transport) throw new Error(`transport with id "${transportId}" not found`);

      const source: ProducerSource = appData.source;
      if (!this.verifyProducerSource(kind, source))
         throw new Error(`Cannot create a producer with source ${source} and kind ${kind}!`);

      appData = { ...appData, participantId: participant.participantId };

      const producer = await transport.produce({
         ...producerOptions,
         kind,
         appData,
         // keyFrameRequestDelay: 5000
      });

      if (participant.producers[source]) {
         participant.producers[source]?.close();
         participant.producers[source] = undefined;
      }

      producer.on('score', (score) => {
         this.signal.sendToConnection(connection.connectionId, 'producerScore', { producerId: producer.id, score });
      });

      connection.producers.set(producer.id, producer);
      participant.producers[source] = producer;

      await this.roomManager.updateParticipant(participant);

      // update streams
      await this.streamInfoRepo.updateStreams(this.participants.values());

      return { id: producer.id };
   }

   public async connectTransport({ payload, meta }: ConnectTransportRequest): Promise<void> {
      const connection = this.connections.get(meta.connectionId);
      if (!connection) throw new Error('Connection was not found');

      const transport = connection.transport.get(payload.transportId);
      if (!transport) throw new Error(`transport with id "${payload.transportId}" not found`);

      logger.debug('connectTransport() | participantId: %s', connection.participantId);

      await transport.connect(payload);
   }

   public async createTransport({
      payload: { sctpCapabilities, forceTcp, producing, consuming },
      meta,
   }: CreateTransportRequest): Promise<CreateTransportResponse> {
      const connection = this.connections.get(meta.connectionId);
      if (!connection) throw new Error('Connection was not found');

      const participant = this.participants.get(connection.participantId);
      if (!participant) throw new Error(`participant with id "${connection.participantId}" not found`);

      logger.debug('createTransport() | participantId: %s', connection.participantId);

      const webRtcTransportOptions: WebRtcTransportOptions = {
         ...config.webRtcTransport.options,
         enableSctp: Boolean(sctpCapabilities),
         numSctpStreams: sctpCapabilities?.numStreams,
         appData: { producing, consuming },
      };

      if (forceTcp) {
         webRtcTransportOptions.enableUdp = false;
         webRtcTransportOptions.enableTcp = true;
      }

      const transport = await this.router.createWebRtcTransport(webRtcTransportOptions);
      connection.transport.set(transport.id, transport);

      const { maxIncomingBitrate } = config.webRtcTransport;

      // If set, apply max incoming bitrate limit.
      if (maxIncomingBitrate) {
         try {
            await transport.setMaxIncomingBitrate(maxIncomingBitrate);
         } catch (error) {}
      }

      await this.roomManager.updateParticipant(participant);

      return {
         id: transport.id,
         iceParameters: transport.iceParameters,
         iceCandidates: transport.iceCandidates,
         dtlsParameters: transport.dtlsParameters,
         sctpParameters: transport.sctpParameters,
      };
   }

   private removeProducer(producer: Producer, participant: Participant): void {
      for (const [k, activeProducer] of Object.entries(participant.producers)) {
         if (activeProducer?.id === producer.id) {
            participant.producers[k as ProducerSource] = undefined;
         }
      }
   }

   private verifyProducerSource(kind: MediaKind, source: ProducerSource): boolean {
      if (source === 'mic' && kind === 'audio') return true;
      if (source === 'screen' && kind === 'video') return true;
      if (source === 'webcam' && kind === 'video') return true;

      return false;
   }
}