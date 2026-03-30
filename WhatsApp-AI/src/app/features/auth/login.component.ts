import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/ui/toast.service';

type LoginMode = 'tenant' | 'host';
type AuthStep = 'login' | 'forgot' | 'verify-otp' | 'reset-password';

const passwordMatchValidator: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const pw = group.get('newPassword')?.value as string;
  const confirm = group.get('confirmPassword')?.value as string;
  return pw && confirm && pw !== confirm ? { passwordMismatch: true } : null;
};

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent implements OnDestroy {
  private readonly subscription = new Subscription();

  // ── Login step ──────────────────────────────────────────
  readonly form;
  mode: LoginMode = 'tenant';
  isSubmitting = false;

  // ── Forgot-password step ────────────────────────────────
  readonly forgotForm;
  isSendingOtp = false;

  // ── Verify-OTP step ─────────────────────────────────────
  readonly otpForm;
  isVerifyingOtp = false;

  // ── Reset-password step ─────────────────────────────────
  readonly resetForm;
  isResetting = false;

  // ── Step machine ────────────────────────────────────────
  step: AuthStep = 'login';

  private forgotTenantName = '';
  private forgotEmail = '';
  private verifiedResetToken = '';

  get maskedEmail(): string {
    const [local, domain] = this.forgotEmail.split('@');
    if (!local || !domain) return this.forgotEmail;
    return `${local.slice(0, 2)}***@${domain}`;
  }

  constructor(
    private readonly formBuilder: FormBuilder,
    private readonly authService: AuthService,
    private readonly router: Router,
    private readonly toastService: ToastService,
    private readonly cdr: ChangeDetectorRef,
  ) {
    this.form = this.formBuilder.nonNullable.group({
      tenantName: ['', [Validators.required]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
    });

    this.forgotForm = this.formBuilder.nonNullable.group({
      tenantName: ['', [Validators.required]],
      email: ['', [Validators.required, Validators.email]],
    });

    this.otpForm = this.formBuilder.nonNullable.group({
      otp: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
    });

    this.resetForm = this.formBuilder.nonNullable.group(
      {
        newPassword: ['', [Validators.required, Validators.minLength(8)]],
        confirmPassword: ['', [Validators.required]],
      },
      { validators: passwordMatchValidator },
    );

    this.subscription.add(
      this.authService.isAuthenticated$.subscribe((isAuthenticated) => {
        if (isAuthenticated) void this.router.navigateByUrl('/dashboard');
      }),
    );
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  // ── Login ────────────────────────────────────────────────

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
    const request = this.mode === 'host' ? { tenantName: 'host', email, password } : { tenantName, email, password };

    this.subscription.add(
      this.authService.login(request).subscribe({
        next: () => {
          this.isSubmitting = false;
          this.toastService.success('Login successful', 'Welcome back.');
          this.cdr.markForCheck();
          void this.router.navigateByUrl('/dashboard');
        },
        error: (error: unknown) => {
          this.isSubmitting = false;
          this.toastService.error('Login failed', this.resolveErrorMessage(error));
          this.cdr.markForCheck();
        },
      }),
    );
  }

  // ── Forgot password ──────────────────────────────────────

  goToForgot(): void {
    this.forgotForm.reset();
    this.step = 'forgot';
  }

  sendOtp(): void {
    if (this.forgotForm.invalid || this.isSendingOtp) {
      this.forgotForm.markAllAsTouched();
      return;
    }

    this.isSendingOtp = true;
    const { tenantName, email } = this.forgotForm.getRawValue();

    this.subscription.add(
      this.authService.forgotPassword(tenantName, email).subscribe({
        next: () => this.afterOtpSent(tenantName, email),
        // Backend always returns 200 — any error here is network-level.
        // Still advance the user so we don't reveal if the email exists.
        error: () => this.afterOtpSent(tenantName, email),
      }),
    );
  }

  private afterOtpSent(tenantName: string, email: string): void {
    this.isSendingOtp = false;
    this.forgotTenantName = tenantName;
    this.forgotEmail = email;
    this.otpForm.reset();
    this.step = 'verify-otp';
    this.cdr.markForCheck();
  }

  // ── Verify OTP ───────────────────────────────────────────

  verifyOtp(): void {
    if (this.otpForm.invalid || this.isVerifyingOtp) {
      this.otpForm.markAllAsTouched();
      return;
    }

    this.isVerifyingOtp = true;
    const { otp } = this.otpForm.getRawValue();

    this.subscription.add(
      this.authService.verifyOtp(this.forgotTenantName, this.forgotEmail, otp).subscribe({
        next: (res) => {
          this.isVerifyingOtp = false;
          this.verifiedResetToken = res.resetToken;
          this.resetForm.reset();
          this.step = 'reset-password';
          this.cdr.markForCheck();
        },
        error: (error: unknown) => {
          this.isVerifyingOtp = false;
          this.toastService.error('Invalid code', this.resolveErrorMessage(error));
          this.cdr.markForCheck();
        },
      }),
    );
  }

  resendOtp(): void {
    if (this.isSendingOtp) return;
    this.isSendingOtp = true;

    this.subscription.add(
      this.authService.forgotPassword(this.forgotTenantName, this.forgotEmail).subscribe({
        next: () => this.afterResend(),
        error: () => this.afterResend(),
      }),
    );
  }

  private afterResend(): void {
    this.isSendingOtp = false;
    this.toastService.success('Code sent', 'A new OTP has been sent to your email.');
    this.otpForm.reset();
    this.cdr.markForCheck();
  }

  // ── Reset password ───────────────────────────────────────

  resetPassword(): void {
    if (this.resetForm.invalid || this.isResetting) {
      this.resetForm.markAllAsTouched();
      return;
    }

    this.isResetting = true;
    const { newPassword } = this.resetForm.getRawValue();

    this.subscription.add(
      this.authService
        .resetPassword(this.forgotTenantName, this.forgotEmail, this.verifiedResetToken, newPassword)
        .subscribe({
          next: () => {
            this.isResetting = false;
            this.toastService.success('Password updated', 'Your password has been reset. Please sign in.');
            this.step = 'login';
            this.cdr.markForCheck();
          },
          error: (error: unknown) => {
            this.isResetting = false;
            this.toastService.error('Reset failed', this.resolveErrorMessage(error));
            this.cdr.markForCheck();
          },
        }),
    );
  }

  // ── Navigation ───────────────────────────────────────────

  backToLogin(): void { this.step = 'login'; }
  backToForgot(): void { this.step = 'forgot'; }
  backToOtp(): void { this.step = 'verify-otp'; }

  // ── Utils ────────────────────────────────────────────────

  private resolveErrorMessage(error: unknown): string {
    if (typeof error === 'string') return error;
    if (error && typeof error === 'object') {
      const e = error as Record<string, unknown>;
      if (e['error'] && typeof e['error'] === 'object') {
        const inner = e['error'] as Record<string, unknown>;
        if (typeof inner['message'] === 'string') return inner['message'];
      }
      if (typeof e['message'] === 'string') return e['message'];
    }
    return 'An unexpected error occurred. Please try again.';
  }
}
