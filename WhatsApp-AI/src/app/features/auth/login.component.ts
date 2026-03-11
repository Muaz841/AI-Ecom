import { CommonModule } from '@angular/common';
import { Component, OnDestroy } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/ui/toast.service';

type LoginMode = 'tenant' | 'host';

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
  mode: LoginMode = 'tenant';
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

  setMode(mode: LoginMode): void {
    if (this.mode === mode) return;
    this.mode = mode;
    const tenantControl = this.form.controls.tenantName;

    if (mode === 'host') {
      tenantControl.clearValidators();
      tenantControl.setValue('');
    } else {
      tenantControl.setValidators([Validators.required]);
    }

    tenantControl.updateValueAndValidity();
    this.form.markAsUntouched();
  }

  submit(): void {
    if (this.form.invalid || this.isSubmitting) {
      this.form.markAllAsTouched();
      this.toastService.warn('Validation', 'Please fill all required fields.');
      return;
    }

    this.isSubmitting = true;
    const { tenantName, email, password } = this.form.getRawValue();

    const request =
      this.mode === 'host'
        ? { tenantName: 'host', email, password }
        : { tenantName, email, password };

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

  private resolveErrorMessage(error: unknown): string {
    if (typeof error === 'string') return error;
    if (error && typeof error === 'object' && 'message' in error && typeof error.message === 'string') {
      return error.message;
    }
    return 'Login failed. Please verify credentials.';
  }
}
