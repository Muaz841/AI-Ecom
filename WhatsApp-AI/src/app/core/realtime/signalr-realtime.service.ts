import { Injectable, NgZone } from '@angular/core';
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
  private readonly registeredEvents = new Set<string>();

  readonly isConnected$ = this.connectedSubject.asObservable();
  readonly events$ = this.eventSubject.asObservable();

  constructor(
    private readonly tokenStore: TokenStore,
    private readonly ngZone: NgZone,
  ) {}

  async connect(path = '/hubs/realtime'): Promise<void> {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    const url = `${APP_CONFIG.apiBaseUrl}${path}`;
    console.log('[SignalR] Connecting to', url);

    this.connection = new HubConnectionBuilder()
      .withUrl(url, {
        transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents,
        accessTokenFactory: () => {
          const token = this.tokenStore.getAccessToken() ?? '';
          console.log('[SignalR] accessTokenFactory called, token present:', !!token);
          return token;
        },
      })
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(LogLevel.Information)
      .build();

    this.connection.onclose((err) => {
      console.warn('[SignalR] Connection closed', err);
      this.ngZone.run(() => this.connectedSubject.next(false));
    });
    this.connection.onreconnected((id) => {
      console.log('[SignalR] Reconnected, connectionId=', id);
      this.ngZone.run(() => this.connectedSubject.next(true));
    });
    this.connection.onreconnecting((err) => {
      console.warn('[SignalR] Reconnecting…', err);
      this.ngZone.run(() => this.connectedSubject.next(false));
    });

    await this.connection.start();
    console.log('[SignalR] Connected ✓, state=', this.connection.state);
    this.connectedSubject.next(true);
  }

  on<TPayload>(eventName: string): void {
    if (!this.connection || this.registeredEvents.has(eventName)) {
      console.log('[SignalR] on() skipped for', eventName,
        '— connection:', !!this.connection, ', already registered:', this.registeredEvents.has(eventName));
      return;
    }

    this.registeredEvents.add(eventName);
    console.log('[SignalR] Registered handler for event:', eventName);
    this.connection.on(eventName, (payload: TPayload) => {
      console.log('[SignalR] Event received:', eventName, payload);
      this.ngZone.run(() => this.eventSubject.next({ event: eventName, payload }));
    });
  }

  async disconnect(): Promise<void> {
    if (!this.connection) {
      return;
    }

    await this.connection.stop();
    this.connectedSubject.next(false);
    this.connection = null;
    this.registeredEvents.clear();
  }
}
