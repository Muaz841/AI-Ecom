import { Injectable } from '@angular/core';
import { MessageService } from 'primeng/api';

@Injectable({ providedIn: 'root' })
export class ToastService {
  private static readonly defaultLifeMs = 3000;

  constructor(private readonly messageService: MessageService) {}

  success(summary: string, detail: string): void {
    this.messageService.add({ severity: 'success', summary, detail, life: ToastService.defaultLifeMs });
  }

  error(summary: string, detail: string): void {
    this.messageService.add({ severity: 'error', summary, detail, life: ToastService.defaultLifeMs });
  }

  warn(summary: string, detail: string): void {
    this.messageService.add({ severity: 'warn', summary, detail, life: ToastService.defaultLifeMs });
  }

  info(summary: string, detail: string): void {
    this.messageService.add({ severity: 'info', summary, detail, life: ToastService.defaultLifeMs });
  }
}
