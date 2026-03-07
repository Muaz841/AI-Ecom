import { Injectable } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
  LogLevel,
} from '@microsoft/signalr';
import { BehaviorSubject, Subject } from 'rxjs';
import { TokenStore } from '../auth/token.store';
import { APP_CONFIG } from '../config/app-config';

@Injectable({ providedIn: 'root' })
export class SignalrRealtimeService {
  private connection: HubConnection | null = null;
  private readonly connectedSubject = new BehaviorSubject<boolean>(false);
  private readonly eventSubject = new Subject<{ event: string; payload: unknown }>();

  readonly isConnected$ = this.connectedSubject.asObservable();
  readonly events$ = this.eventSubject.asObservable();

  constructor(private readonly tokenStore: TokenStore) {}

  async connect(path = '/hubs/realtime'): Promise<void> {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(`${APP_CONFIG.apiBaseUrl}${path}`, {
        transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents,
        accessTokenFactory: () => this.tokenStore.getAccessToken() ?? '',
      })
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.onclose(() => this.connectedSubject.next(false));
    this.connection.onreconnected(() => this.connectedSubject.next(true));
    this.connection.onreconnecting(() => this.connectedSubject.next(false));

    await this.connection.start();
    this.connectedSubject.next(true);
  }

  on<TPayload>(eventName: string): void {
    if (!this.connection) {
      return;
    }

    this.connection.on(eventName, (payload: TPayload) => {
      this.eventSubject.next({ event: eventName, payload });
    });
  }

  async disconnect(): Promise<void> {
    if (!this.connection) {
      return;
    }

    await this.connection.stop();
    this.connectedSubject.next(false);
    this.connection = null;
  }
}
