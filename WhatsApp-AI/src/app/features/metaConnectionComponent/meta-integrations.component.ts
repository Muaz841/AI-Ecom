import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';
import {
  IntegrationStatus,
  MetaChannel,
  MetaConnectionView,
} from '../../core/integrations/meta-integration.models';
import { MetaIntegrationService } from '../../core/integrations/meta-integration.service';
import { ToastService } from '../../core/ui/toast.service';
import { IntegrationCardComponent } from './components/integration-card/integration-card.component';

interface ChannelCard {
  platform: MetaChannel;
  title: string;
  description: string;
  icon: string;
}

@Component({
  selector: 'app-meta-integrations',
  standalone: true,
  imports: [
    CommonModule,
    ToolbarModule,
    ButtonModule,
    TableModule,
    TagModule,
    DialogModule,
    ProgressSpinnerModule,
    TooltipModule,
    IntegrationCardComponent,
  ],
  templateUrl: './meta-integrations.component.html',
  styleUrl: './meta-integrations.component.scss',
})
export class MetaIntegrationsComponent implements OnInit, OnDestroy {
  private readonly subscription = new Subscription();

  readonly channels: ChannelCard[] = [
    {
      platform: 'instagram',
      title: 'Instagram Direct Messages',
      description: 'Connect your Instagram business account to automate replies and manage conversations.',
      icon: 'pi pi-instagram',
    },
    {
      platform: 'facebook',
      title: 'Facebook Messenger',
      description: 'Connect your Facebook page to respond to Messenger chats automatically.',
      icon: 'pi pi-facebook',
    },
    {
      platform: 'whatsapp',
      title: 'WhatsApp Business',
      description: 'Connect your WhatsApp Business account to automate customer support.',
      icon: 'pi pi-whatsapp',
    },
  ];

  rows: MetaConnectionView[] = [];
  isLoading = false;
  isConnecting: MetaChannel | null = null;
  disconnectingConnectionId: string | null = null;
  showDisconnectDialog = false;
  pendingDisconnectConnectionId: string | null = null;

  constructor(
    private readonly integrationService: MetaIntegrationService,
    private readonly toastService: ToastService,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadConnections();
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  connect(channel: MetaChannel): void {
    if (this.isConnecting) {
      return;
    }

    this.isConnecting = channel;
    const callbackUrl = `${window.location.origin}/integrations/meta/callback`;

    this.subscription.add(
      this.integrationService.startConnect(channel, callbackUrl).subscribe({
        next: (authorizationUrl) => {
          window.location.href = authorizationUrl;
        },
        error: (error: unknown) => {
          this.isConnecting = null;
          this.toastService.error('Connection failed', this.resolveError(error));
        },
      }),
    );
  }

  disconnect(connectionId: string): void {
    if (!connectionId) {
      return;
    }

    this.pendingDisconnectConnectionId = connectionId;
    this.showDisconnectDialog = true;
  }

  disconnectChannel(channel: MetaChannel): void {
    const row = this.rows.find((item) => item.platform === channel);
    if (!row) {
      return;
    }

    this.disconnect(row.id);
  }

  confirmDisconnect(): void {
    if (!this.pendingDisconnectConnectionId || this.disconnectingConnectionId) {
      return;
    }

    const connectionId = this.pendingDisconnectConnectionId;
    this.disconnectingConnectionId = connectionId;
    this.showDisconnectDialog = false;
    this.subscription.add(
      this.integrationService.disconnect(connectionId).subscribe({
        next: () => {
          this.disconnectingConnectionId = null;
          this.pendingDisconnectConnectionId = null;
          this.toastService.success('Disconnected', 'Integration disconnected successfully.');
          this.loadConnections();
        },
        error: (error: unknown) => {
          this.disconnectingConnectionId = null;
          this.pendingDisconnectConnectionId = null;
          this.toastService.error('Disconnect failed', this.resolveError(error));
        },
      }),
    );
  }

  cancelDisconnect(): void {
    this.showDisconnectDialog = false;
    this.pendingDisconnectConnectionId = null;
  }

  statusForChannel(channel: MetaChannel): IntegrationStatus {
    const channelRows = this.rows.filter((row) => row.platform === channel);
    if (channelRows.length === 0) {
      return 'not_connected';
    }

    if (channelRows.some((row) => row.status === 'connected' || (row.status as string) === 'active')) {
      return 'connected';
    }

    return 'expired';
  }

  private loadConnections(): void {
    this.isLoading = true;
    this.subscription.add(
      this.integrationService.listConnectionsView().subscribe({
        next: (rows) => {
          this.isLoading = false;
          this.rows = rows;
          this.isConnecting = null;
          this.cdr.markForCheck();
        },
        error: (error: unknown) => {
          this.isLoading = false;
          this.rows = [];
          this.cdr.markForCheck();
          this.toastService.error('Load failed', this.resolveError(error));
        },
      }),
    );
  }

  private resolveError(error: unknown): string {
    if (typeof error === 'string') {
      return error;
    }

    if (error && typeof error === 'object' && 'message' in error && typeof error.message === 'string') {
      return error.message;
    }

    return 'Unable to complete request.';
  }
}
