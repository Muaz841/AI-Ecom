import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { BadgeModule } from 'primeng/badge';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { DividerModule } from 'primeng/divider';
import { InputNumberModule } from 'primeng/inputnumber';
import { AiProfileService, TenantAIProfileResult } from './ai-profile.service';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-ai-profile',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CardModule,
    ButtonModule,
    InputTextModule,
    TextareaModule,
    ToastModule,
    ToolbarModule,
    BadgeModule,
    TagModule,
    TooltipModule,
    DividerModule,
    InputNumberModule,
  ],
  providers: [MessageService],
  templateUrl: './ai-profile.component.html',
  styleUrl: './ai-profile.component.scss',
})
export class AiProfileComponent implements OnInit {
  private readonly service = inject(AiProfileService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(MessageService);

  loading = signal(false);
  saving = signal(false);
  profile = signal<TenantAIProfileResult | null>(null);

  form!: FormGroup;

  ngOnInit(): void {
    this.form = this.fb.group({
      systemPrompt: ['', [Validators.required, Validators.minLength(10)]],
      tone: [''],
      language: [''],
      brandRules: [''],
      forbiddenTopics: [''],
      defaultResponseStyle: [''],
      aiCallsPerHourLimit: [200, [Validators.required, Validators.min(0), Validators.max(10000)]],
    });

    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.service.getProfile().subscribe({
      next: (result) => {
        this.profile.set(result);
        this.patchForm(result);
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        // 204 No Content = no profile yet, that's fine
        if (err.status !== 204 && err.status !== 404) {
          this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to load AI profile.' });
        }
        this.loading.set(false);
      },
    });
  }

  private patchForm(p: TenantAIProfileResult): void {
    this.form.patchValue({
      systemPrompt: p.systemPrompt,
      tone: p.tone ?? '',
      language: p.language ?? '',
      brandRules: p.brandRules ?? '',
      forbiddenTopics: p.forbiddenTopics ?? '',
      defaultResponseStyle: p.defaultResponseStyle ?? '',
      aiCallsPerHourLimit: p.aiCallsPerHourLimit,
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    this.saving.set(true);
    this.service
      .saveProfile({
        systemPrompt: v.systemPrompt,
        tone: v.tone || null,
        language: v.language || null,
        brandRules: v.brandRules || null,
        forbiddenTopics: v.forbiddenTopics || null,
        defaultResponseStyle: v.defaultResponseStyle || null,
        aiCallsPerHourLimit: v.aiCallsPerHourLimit,
      })
      .subscribe({
        next: (result) => {
          this.profile.set(result);
          this.toast.add({ severity: 'success', summary: 'Saved', detail: 'AI persona profile updated.' });
          this.saving.set(false);
        },
        error: () => {
          this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to save AI profile.' });
          this.saving.set(false);
        },
      });
  }

  get systemPromptControl() { return this.form.get('systemPrompt'); }
  get aiCallsControl() { return this.form.get('aiCallsPerHourLimit'); }
}
