import {
  Component, OnInit, ViewChild, inject, signal, DestroyRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of } from 'rxjs';
import { MessageService } from 'primeng/api';
import { Popover } from 'primeng/popover';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { TextareaModule } from 'primeng/textarea';
import { InputTextModule } from 'primeng/inputtext';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { PopoverModule } from 'primeng/popover';
import { DividerModule } from 'primeng/divider';
import { BadgeModule } from 'primeng/badge';
import { ContentAiService, PoseSummary, GenerateFromNewPoseResponse } from './content-ai.service';
import { AiProfileService, TenantAIProfileResult } from '../ai-profile/ai-profile.service';
import { APP_CONFIG } from '../../core/config/app-config';

export interface QueueItem {
  poseId: string;
  poseName: string;
  referenceImagePath: string;
  status: 'pending' | 'processing' | 'done' | 'error';
  resultBase64?: string;
  resultMimeType?: string;
  errorMessage?: string;
}

@Component({
  selector: 'app-content-ai',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    ToastModule,
    TextareaModule,
    InputTextModule,
    SkeletonModule,
    TagModule,
    TooltipModule,
    PopoverModule,
    DividerModule,
    BadgeModule,
  ],
  providers: [MessageService],
  templateUrl: './content-ai.component.html',
  styleUrl: './content-ai.component.scss',
})
export class ContentAiComponent implements OnInit {
  @ViewChild('settingsPopover') settingsPopover!: Popover;

  private readonly service    = inject(ContentAiService);
  private readonly profileSvc = inject(AiProfileService);
  private readonly fb         = inject(FormBuilder);
  private readonly toast      = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);

  readonly apiBaseUrl = APP_CONFIG.apiBaseUrl;

  // ── Pose library ────────────────────────────────────────────────────────────
  poses        = signal<PoseSummary[]>([]);
  posesLoading = signal(false);

  // ── Main pipeline state ─────────────────────────────────────────────────────
  selectedPoseIds = signal<Set<string>>(new Set());
  dressFile       = signal<File | null>(null);
  dressPreview    = signal<string | null>(null);

  // ── Queue / results ─────────────────────────────────────────────────────────
  queueItems     = signal<QueueItem[]>([]);
  isGenerating   = signal(false);
  completedCount = signal(0);

  // ── Settings popover ─────────────────────────────────────────────────────────
  openSettingsPopover(event: Event): void {
    this.settingsPopover.toggle(event);
  }

  openNewPoseFromMenu(event: Event): void {
    this.settingsPopover.hide();
    this.openNewPoseDialog();
  }

  openPromptSettingsFromMenu(event: Event): void {
    this.settingsPopover.hide();
    this.openPromptSettingsDialog();
  }

  // ── New Pose dialog ─────────────────────────────────────────────────────────
  showNewPoseDialog   = signal(false);
  newPosePhase        = signal<'input' | 'review'>('input');
  newPosePoseFile     = signal<File | null>(null);
  newPoseDressFile    = signal<File | null>(null);
  newPosePosePreview  = signal<string | null>(null);
  newPoseDressPreview = signal<string | null>(null);
  newPoseGenerating   = signal(false);
  newPoseResult       = signal<GenerateFromNewPoseResponse | null>(null);
  newPoseSaving       = signal(false);
  poseNameForm!: FormGroup;

  // ── Prompt Settings dialog ──────────────────────────────────────────────────
  showPromptDialog = signal(false);
  promptForm!: FormGroup;
  promptSaving     = signal(false);
  promptLoading    = signal(false);

  // ── Lifecycle ───────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.poseNameForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(60)]],
    });
    this.promptForm = this.fb.group({
      poseExtractionPrompt:  [''],
      imageGenerationPrompt: [''],
    });
    this.loadPoses();
  }

  // ── Poses ───────────────────────────────────────────────────────────────────
  loadPoses(): void {
    this.posesLoading.set(true);
    this.service.listPoses().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next:  (poses) => { this.poses.set(poses); this.posesLoading.set(false); },
      error: ()      => { this.posesLoading.set(false); },
    });
  }

  togglePose(id: string): void {
    const next = new Set(this.selectedPoseIds());
    next.has(id) ? next.delete(id) : next.add(id);
    this.selectedPoseIds.set(next);
  }

  isPoseSelected(id: string): boolean { return this.selectedPoseIds().has(id); }

  get selectedCount(): number { return this.selectedPoseIds().size; }

  get progressPercent(): number {
    const total = this.queueItems().length;
    return total === 0 ? 0 : Math.round((this.completedCount() / total) * 100);
  }

  poseImageUrl(path: string): string {
    return `${this.apiBaseUrl}${path}`;
  }

  // ── Dress image ─────────────────────────────────────────────────────────────
  onDressFileChange(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.dressFile.set(file);
    const reader = new FileReader();
    reader.onload = (e) => this.dressPreview.set(e.target?.result as string);
    reader.readAsDataURL(file);
  }

  clearDressFile(): void {
    this.dressFile.set(null);
    this.dressPreview.set(null);
  }

  // ── Queue generation ─────────────────────────────────────────────────────────
  async generate(): Promise<void> {
    const file        = this.dressFile();
    const selectedIds = Array.from(this.selectedPoseIds());
    if (!file || selectedIds.length === 0 || this.isGenerating()) return;

    const allPoses = this.poses();
    const items: QueueItem[] = selectedIds.map((id) => {
      const pose = allPoses.find((p) => p.id === id)!;
      return { poseId: id, poseName: pose.name, referenceImagePath: pose.referenceImagePath, status: 'pending' };
    });

    this.queueItems.set(items);
    this.isGenerating.set(true);
    this.completedCount.set(0);

    for (let i = 0; i < items.length; i++) {
      items[i].status = 'processing';
      this.queueItems.set([...items]);

      try {
        const result = await firstValueFrom(this.service.generateFromSavedPose(items[i].poseId, file));
        items[i].status        = 'done';
        items[i].resultBase64  = result.generatedImageBase64;
        items[i].resultMimeType = result.generatedImageMimeType;
      } catch (err: any) {
        items[i].status       = 'error';
        items[i].errorMessage = err?.error?.message ?? 'Generation failed.';
      }

      this.completedCount.set(i + 1);
      this.queueItems.set([...items]);
    }

    this.isGenerating.set(false);
    const doneCount = items.filter((x) => x.status === 'done').length;
    this.toast.add({
      severity: doneCount === items.length ? 'success' : 'warn',
      summary:  'Complete',
      detail:   `${doneCount} of ${items.length} images generated.`,
    });
  }

  downloadImage(item: QueueItem): void {
    if (!item.resultBase64 || !item.resultMimeType) return;
    const a  = document.createElement('a');
    a.href   = `data:${item.resultMimeType};base64,${item.resultBase64}`;
    a.download = `${item.poseName.replace(/\s+/g, '_')}_model_shot.png`;
    a.click();
  }

  retryItem(item: QueueItem): void {
    const file = this.dressFile();
    if (!file || this.isGenerating()) return;
    this.runSingleItem(item, file);
  }

  private async runSingleItem(item: QueueItem, file: File): Promise<void> {
    item.status = 'processing';
    item.errorMessage = undefined;
    this.queueItems.set([...this.queueItems()]);
    try {
      const result = await firstValueFrom(this.service.generateFromSavedPose(item.poseId, file));
      item.status        = 'done';
      item.resultBase64  = result.generatedImageBase64;
      item.resultMimeType = result.generatedImageMimeType;
    } catch (err: any) {
      item.status       = 'error';
      item.errorMessage = err?.error?.message ?? 'Generation failed.';
    }
    this.queueItems.set([...this.queueItems()]);
  }

  clearResults(): void {
    this.queueItems.set([]);
    this.completedCount.set(0);
  }

  // ── New Pose dialog ─────────────────────────────────────────────────────────
  openNewPoseDialog(): void {
    this.newPosePhase.set('input');
    this.newPosePoseFile.set(null);
    this.newPoseDressFile.set(null);
    this.newPosePosePreview.set(null);
    this.newPoseDressPreview.set(null);
    this.newPoseResult.set(null);
    this.poseNameForm.reset();
    this.showNewPoseDialog.set(true);
  }

  onNewPosePoseFileChange(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.newPosePoseFile.set(file);
    const reader = new FileReader();
    reader.onload = (e) => this.newPosePosePreview.set(e.target?.result as string);
    reader.readAsDataURL(file);
  }

  onNewPoseDressFileChange(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.newPoseDressFile.set(file);
    const reader = new FileReader();
    reader.onload = (e) => this.newPoseDressPreview.set(e.target?.result as string);
    reader.readAsDataURL(file);
  }

  async generateNewPose(): Promise<void> {
    const poseFile  = this.newPosePoseFile();
    const dressFile = this.newPoseDressFile();
    if (!poseFile || !dressFile || this.newPoseGenerating()) return;

    this.newPoseGenerating.set(true);
    try {
      const result = await firstValueFrom(this.service.generateFromNewPose(poseFile, dressFile));
      this.newPoseResult.set(result);
      this.newPosePhase.set('review');
    } catch (err: any) {
      this.toast.add({ severity: 'error', summary: 'Failed', detail: err?.error?.message ?? 'Pose extraction failed.' });
    } finally {
      this.newPoseGenerating.set(false);
    }
  }

  async saveNewPose(): Promise<void> {
    if (this.poseNameForm.invalid) { this.poseNameForm.markAllAsTouched(); return; }
    const result   = this.newPoseResult();
    const poseFile = this.newPosePoseFile();
    if (!result || !poseFile || this.newPoseSaving()) return;

    this.newPoseSaving.set(true);
    try {
      await firstValueFrom(this.service.savePose(result.sessionToken, this.poseNameForm.value.name.trim(), poseFile));
      this.toast.add({ severity: 'success', summary: 'Saved', detail: 'Pose added to library.' });
      this.showNewPoseDialog.set(false);
      this.loadPoses();
    } catch {
      this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to save pose.' });
    } finally {
      this.newPoseSaving.set(false);
    }
  }

  // ── Prompt Settings dialog ──────────────────────────────────────────────────
  openPromptSettingsDialog(): void {
    this.showPromptDialog.set(true);
    this.promptLoading.set(true);
    this.profileSvc.getProfile().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (profile) => {
        this.promptForm.patchValue({
          poseExtractionPrompt:  profile.poseExtractionPrompt  ?? '',
          imageGenerationPrompt: profile.imageGenerationPrompt ?? '',
        });
        this.promptLoading.set(false);
      },
      error: () => this.promptLoading.set(false),
    });
  }

  savePromptSettings(): void {
    if (this.promptSaving()) return;
    const v = this.promptForm.value;
    this.promptSaving.set(true);

    this.profileSvc.getProfile().pipe(
      catchError(() => of(null as TenantAIProfileResult | null)),
      switchMap((existing) => {
        if (!existing?.systemPrompt) {
          throw new Error('Please configure your AI Persona profile (System Prompt) first.');
        }
        return this.profileSvc.saveProfile({
          systemPrompt:          existing.systemPrompt,
          tone:                  existing.tone,
          language:              existing.language,
          brandRules:            existing.brandRules,
          forbiddenTopics:       existing.forbiddenTopics,
          defaultResponseStyle:  existing.defaultResponseStyle,
          aiCallsPerHourLimit:   existing.aiCallsPerHourLimit,
          poseExtractionPrompt:  v.poseExtractionPrompt  || null,
          imageGenerationPrompt: v.imageGenerationPrompt || null,
        });
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: () => {
        this.toast.add({ severity: 'success', summary: 'Saved', detail: 'Prompt settings saved.' });
        this.promptSaving.set(false);
        this.showPromptDialog.set(false);
      },
      error: (err: Error) => {
        this.toast.add({ severity: 'error', summary: 'Error', detail: err.message ?? 'Failed to save.' });
        this.promptSaving.set(false);
      },
    });
  }

  get poseNameControl() { return this.poseNameForm.get('name'); }
}
