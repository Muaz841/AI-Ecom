import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TagModule } from 'primeng/tag';
import { DividerModule } from 'primeng/divider';
import { WebhookTesterService, WebhookTestRequest, WebhookTestResponse } from './webhook-tester.service';
import { MetaIntegrationService } from '../../../core/integrations/meta-integration.service';
import { MetaConnection, MetaAsset } from '../../../core/integrations/meta-integration.models';
import { ToastService } from '../../../core/ui/toast.service';
import { MessageType } from '../../../shared/constants/message.constants';

interface SelectOption {
  label: string;
  value: string;
}

@Component({
  selector: 'app-webhook-tester',
  standalone: true,
  templateUrl: './webhook-tester.component.html',
  styleUrls: ['./webhook-tester.component.scss'],
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    TextareaModule,
    SelectModule,
    ToggleSwitchModule,
    TagModule,
    DividerModule,
  ],
})
export class WebhookTesterComponent {
  readonly platformOptions: SelectOption[] = [
    { label: 'WhatsApp', value: 'whatsapp' },
    { label: 'Instagram DM', value: 'instagram' },
    { label: 'Facebook DM', value: 'facebook' },
  ];

  readonly messageTypeOptions: SelectOption[] = [
    { label: 'Text Message', value: MessageType.Text },
    { label: 'Comment',      value: MessageType.Comment },
  ];


  platform       = 'whatsapp';
  from           = 'user_123456';
  to             = '';
  message        = 'Hello! I need help with my order.';
  messageType    = MessageType.Text;
  useRawPayload  = false;
  rawPayloadJson = '';
  selectedAsset  = '';

  
  sending    = signal(false);
  response   = signal<WebhookTestResponse | null>(null);
  hasResult  = signal(false);
  assets     = signal<MetaConnection[]>([]);

  readonly assetOptions = computed(() => {
    const platform = this.platform;
    const rows = this.assets();
    const options: SelectOption[] = [];
    for (const connection of rows) {
      if (connection.channel?.toLowerCase() !== platform) continue;
      for (const asset of connection.assets ?? []) {
        options.push({
          label: `${asset.externalName ?? 'Asset'} (${asset.assetType})`,
          value: asset.externalId,
        });
      }
    }
    return options;
  });

  constructor(
    private readonly service: WebhookTesterService,
    private readonly integrations: MetaIntegrationService,
    private readonly toast: ToastService,
  ) {}

  ngOnInit(): void {
    this.integrations.listConnections().subscribe({
      next: (rows) => this.assets.set(rows),
      error: () => this.assets.set([]),
    });
  }

  send(): void {
    if (this.sending()) return;
    if (!this.to.trim() && !this.useRawPayload) {
      this.toast.error('Validation', 'Business Asset ID (To) is required for tenant resolution.');
      return;
    }

    this.sending.set(true);
    this.response.set(null);
    this.hasResult.set(false);

    const req: WebhookTestRequest = {
      platform:       this.platform,
      from:           this.from.trim(),
      to:             this.to.trim(),
      message:        this.message,
      messageType:    this.messageType,
      useRawPayload:  this.useRawPayload,
      rawPayloadJson: this.useRawPayload ? this.rawPayloadJson : null,
    };

    this.service.sendTest(req).subscribe({
      next: (res) => {
        this.sending.set(false);
        this.response.set(res);
        this.hasResult.set(true);
        if (res.success) {
          this.toast.success('Success', 'Webhook processed successfully.');
        } else {
          this.toast.warn('Failed', res.resultMessage);
        }
      },
      error: () => {
        this.sending.set(false);
        this.toast.error('Error', 'Request failed — check that the backend is running in Development mode.');
      },
    });
  }

  reset(): void {
    this.response.set(null);
    this.hasResult.set(false);
  }

  applySelectedAsset(): void {
    if (this.selectedAsset) {
      this.to = this.selectedAsset;
    }
  }

  formatJson(raw: string | null | undefined): string {
    if (!raw) return '';
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  }
}
