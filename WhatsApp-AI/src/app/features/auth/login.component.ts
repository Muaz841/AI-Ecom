import { CommonModule } from '@angular/common';
import { Component, OnDestroy } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/ui/toast.service';

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

  constructor(
    private readonly formBuilder: FormBuilder,
    private readonly authService: AuthService,
    private readonly router: Router,
    private readonly toastService: ToastService,
  ) {
    this.form = this.formBuilder.nonNullable.group({
      tenantName: ['', [Validators.required]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
    });

    this.subscription.add(
      this.authService.isAuthenticated$.subscribe((isAuthenticated) => {
        if (isAuthenticated) {
          void this.router.navigateByUrl('/dashboard');
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
      this.toastService.warn('Validation', 'Please fill all required login fields.');
      return;
    }

    this.isSubmitting = true;

    const request = this.form.getRawValue();
    this.subscription.add(
      this.authService.login(request).subscribe({
        next: () => {
          this.isSubmitting = false;
          this.toastService.success('Login successful', 'Welcome back.');
          void this.router.navigateByUrl('/dashboard');
        },
        error: (error: unknown) => {
          this.isSubmitting = false;
          this.toastService.error('Login failed', this.resolveErrorMessage(error));
        },
      }),
    );
  }

  continueWithGoogle(): void {
    this.toastService.info('Coming soon', 'Google SSO will be enabled in onboarding.');
  }

  connectInstagram(): void {
    this.toastService.info('Coming soon', 'Instagram OAuth will be enabled after Meta onboarding setup.');
  }

  connectWhatsApp(): void {
    this.toastService.info('Coming soon', 'WhatsApp OAuth will be enabled after Meta onboarding setup.');
  }

  private resolveErrorMessage(error: unknown): string {
    if (typeof error === 'string') {
      return error;
    }

    if (error && typeof error === 'object' && 'message' in error && typeof error.message === 'string') {
      return error.message;
    }

    return 'Login failed. Please verify credentials and tenant name.';
  }
}
