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
  @Input({ required: true }) status: 'connected' | 'not_connected' | 'expired' = 'not_connected';
  @Input({ required: true }) icon!: string;
  @Input() isBusy = false;

  @Output() connect = new EventEmitter<void>();
  @Output() disconnect = new EventEmitter<void>();

  get statusLabel(): string {
    switch (this.status) {
      case 'connected':
        return 'Connected';
      case 'expired':
        return 'Expired';
      default:
        return 'Not Connected';
    }
  }

  get statusSeverity(): 'success' | 'warn' | 'danger' {
    switch (this.status) {
      case 'connected':
        return 'success';
      case 'expired':
        return 'danger';
      default:
        return 'warn';
    }
  }

  onPrimaryAction(): void {
    if (this.isBusy) {
      return;
    }

    if (this.status === 'connected') {
      this.disconnect.emit();
      return;
    }

    this.connect.emit();
  }

  get actionLabel(): string {
    if (this.status === 'connected') {
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
