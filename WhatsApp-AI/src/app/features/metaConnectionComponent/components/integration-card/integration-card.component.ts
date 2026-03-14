import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { MetaChannel } from '../../../../core/integrations/meta-integration.models';

@Component({
  selector: 'app-integration-card',
  standalone: true,
  imports: [CommonModule, ButtonModule, TagModule],
  templateUrl: './integration-card.component.html',
  styleUrl: './integration-card.component.scss',
})
export class IntegrationCardComponent {
  @Input({ required: true }) platform!: MetaChannel;
  @Input({ required: true }) title!: string;
  @Input({ required: true }) description!: string;
  @Input({ required: true }) status: 'connected' | 'active' | 'not_connected' | 'expired' = 'not_connected';
  @Input({ required: true }) icon!: string;
  @Input() isBusy = false;

  @Output() connect = new EventEmitter<void>();
  @Output() disconnect = new EventEmitter<void>();

  get isConnected(): boolean {
    return this.status === 'connected' || this.status === 'active';
  }

  get statusLabel(): string {
    if (this.isConnected) return 'Connected';
    if (this.status === 'expired') return 'Expired';
    return 'Not Connected';
  }

  get statusSeverity(): 'success' | 'warn' | 'danger' {
    if (this.isConnected) return 'success';
    if (this.status === 'expired') return 'danger';
    return 'warn';
  }

  onPrimaryAction(): void {
    if (this.isBusy) {
      return;
    }

    if (this.isConnected) {
      this.disconnect.emit();
      return;
    }

    this.connect.emit();
  }

  get actionLabel(): string {
    if (this.isConnected) {
      return 'Disconnect';
    }

    switch (this.platform) {
      case 'instagram':
        return 'Connect Instagram';
      case 'facebook':
        return 'Connect Facebook';
      default:
        return 'Connect WhatsApp';
    }
  }
}
