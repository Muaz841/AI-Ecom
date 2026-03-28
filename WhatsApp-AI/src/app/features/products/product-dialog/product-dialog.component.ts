import { Component, OnInit, inject, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { InputNumberModule } from 'primeng/inputnumber';
import { DividerModule } from 'primeng/divider';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import {
  ProductsService,
  ProductDetail,
  ProductVariantRequest,
} from '../products.service';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-product-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    TextareaModule,
    InputNumberModule,
    DividerModule,
    TagModule,
    TooltipModule,
  ],
  templateUrl: './product-dialog.component.html',
})
export class ProductDialogComponent implements OnInit {
  private readonly service = inject(ProductsService);
  private readonly fb      = inject(FormBuilder);

  product = input<ProductDetail | null>(null);
  saved   = output<void>();
  cancel  = output<void>();

  saving = false;
  form!: FormGroup;

  get isEdit(): boolean { return !!this.product(); }
  get variantsArray(): FormArray { return this.form.get('variants') as FormArray; }

  ngOnInit(): void {
    const p = this.product();
    this.form = this.fb.group({
      name:        [p?.name ?? '',        [Validators.required, Validators.maxLength(300)]],
      description: [p?.description ?? ''],
      basePrice:   [p?.basePrice ?? null, [Validators.required, Validators.min(0.01)]],
      currency:    [p?.currency ?? 'PKR', [Validators.required, Validators.minLength(3), Validators.maxLength(3)]],
      sku:         [p?.sku ?? ''],
      variants:    this.fb.array(
        (p?.variants ?? []).map(v => this.buildVariantGroup(v))
      ),
    });
  }

  private buildVariantGroup(v?: Partial<ProductVariantRequest & { id?: string }>) {
    return this.fb.group({
      id:            [v?.id ?? null],
      size:          [v?.size ?? '',   Validators.required],
      color:         [v?.color ?? ''],
      stock:         [v?.stock ?? 0,   [Validators.required, Validators.min(0)]],
      priceOverride: [v?.priceOverride ?? null],
    });
  }

  addVariant(): void {
    this.variantsArray.push(this.buildVariantGroup());
  }

  removeVariant(index: number): void {
    this.variantsArray.removeAt(index);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.value;
    const variants: ProductVariantRequest[] = v.variants.map((vr: any) => ({
      id:            vr.id || undefined,
      size:          vr.size,
      color:         vr.color || null,
      stock:         vr.stock,
      priceOverride: vr.priceOverride || null,
    }));

    this.saving = true;
    const p = this.product();

    const payload = {
      name:        v.name,
      description: v.description || null,
      basePrice:   v.basePrice,
      currency:    v.currency,
      sku:         v.sku || null,
      variants:    variants.length ? variants : undefined,
    };

    const req$ = p
      ? this.service.update(p.id, payload)
      : this.service.create(payload);

    req$.subscribe({
      next: () => {
        this.saving = false;
        this.saved.emit();
      },
      error: (err: HttpErrorResponse) => {
        this.saving = false;
        console.error('Save failed', err);
      },
    });
  }
}
