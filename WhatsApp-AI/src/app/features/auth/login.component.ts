import { CommonModule } from '@angular/common';
import { Component, OnDestroy } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent implements OnDestroy {
  private readonly subscription = new Subscription();

  readonly form;

  isSubmitting = false;
  serverError: string | null = null;

  constructor(
    private readonly formBuilder: FormBuilder,
    private readonly authService: AuthService,
    private readonly router: Router,
  ) {
    this.form = this.formBuilder.nonNullable.group({
      clientId: ['', [Validators.required]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
    });

    this.subscription.add(
      this.authService.isAuthenticated$.subscribe((isAuthenticated) => {
        if (isAuthenticated) {
          void this.router.navigateByUrl('/');
        }
      }),
    );
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  submit(): void {
    if (this.form.invalid || this.isSubmitting) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.serverError = null;

    const request = this.form.getRawValue();
    this.subscription.add(
      this.authService.login(request).subscribe({
        next: () => {
          this.isSubmitting = false;
          void this.router.navigateByUrl('/');
        },
        error: (error: unknown) => {
          this.isSubmitting = false;
          this.serverError = this.resolveErrorMessage(error);
        },
      }),
    );
  }

  continueWithGoogle(): void {
    this.serverError = 'Google SSO will be enabled in the onboarding phase.';
  }

  connectInstagram(): void {
    this.serverError = 'Instagram OAuth will be enabled after Meta onboarding setup.';
  }

  connectWhatsApp(): void {
    this.serverError = 'WhatsApp OAuth will be enabled after Meta onboarding setup.';
  }

  private resolveErrorMessage(error: unknown): string {
    if (typeof error === 'string') {
      return error;
    }

    if (error && typeof error === 'object' && 'message' in error && typeof error.message === 'string') {
      return error.message;
    }

    return 'Login failed. Please verify credentials and tenant ID.';
  }
}
