import { Injectable } from '@angular/core';
import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest,
} from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { ToastService } from '../ui/toast.service';

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  constructor(private readonly toastService: ToastService) {}

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(request).pipe(
      catchError((error: unknown) => {
        if (error instanceof HttpErrorResponse) {
          const message = this.extractMessage(error);
          this.toastService.error('Request failed', message);

          const normalizedError = {
            status: error.status,
            message,
            url: error.url,
          };

          return throwError(() => normalizedError);
        }

        return throwError(() => error);
      }),
    );
  }

  private extractMessage(error: HttpErrorResponse): string {
    if (typeof error.error === 'string' && error.error.trim().length > 0) {
      return error.error;
    }

    if (error.error?.message) {
      return String(error.error.message);
    }

    return error.message || 'Unexpected API error';
  }
}
