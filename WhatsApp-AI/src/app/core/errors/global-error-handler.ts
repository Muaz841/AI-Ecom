import { ErrorHandler, Injectable } from '@angular/core';
import { ToastService } from '../ui/toast.service';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  constructor(private readonly toastService: ToastService) {}

  handleError(error: unknown): void {
    const message = this.resolveMessage(error);
    this.toastService.exception('Unexpected Error', message);
    console.error(error);
  }

  private resolveMessage(error: unknown): string {
    if (typeof error === 'string' && error.trim().length > 0) {
      return error;
    }

    if (error && typeof error === 'object' && 'message' in error) {
      const message = (error as { message?: unknown }).message;
      if (typeof message === 'string' && message.trim().length > 0) {
        return message;
      }
    }

    return 'An unexpected client-side error occurred.';
  }
}
