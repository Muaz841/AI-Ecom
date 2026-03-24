import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged, Subject, switchMap, of } from 'rxjs';
import { MessageService, ConfirmationService } from 'primeng/api';
import { TableModule } from 'primeng/table';
import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { TooltipModule } from 'primeng/tooltip';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { DividerModule } from 'primeng/divider';
import { InputNumberModule } from 'primeng/inputnumber';
import {
  ProductsService,
  ProductSummary,
  ProductDetail,
  ProductImageDetail,
  AddImageRequest,
} from './products.service';
import { ProductDialogComponent } from './product-dialog/product-dialog.component';

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    CardModule,
    ButtonModule,
    InputTextModule,
    TagModule,
    ToastModule,
    DialogModule,
    ConfirmDialogModule,
    TooltipModule,
    ProgressSpinnerModule,
    DividerModule,
    InputNumberModule,
    ProductDialogComponent,
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './products.component.html',
  styleUrl: './products.component.scss',
})
export class ProductsComponent implements OnInit {
  private readonly service       = inject(ProductsService);
  private readonly toast         = inject(MessageService);
  private readonly confirmSvc    = inject(ConfirmationService);
  private readonly searchSubject = new Subject<string>();

  // ── State ────────────────────────────────────────────────────────────────

  products   = signal<ProductSummary[]>([]);
  totalCount = signal(0);
  loading    = signal(false);

  pageIndex = 0;
  pageSize  = 20;
  search    = '';

  // ── Dialogs ──────────────────────────────────────────────────────────────

  showFormDialog    = false;
  showImagesDialog  = false;
  editingProduct    = signal<ProductDetail | null>(null);
  imagesProduct     = signal<ProductDetail | null>(null);
  newImageUrl       = '';
  newImageAlt       = '';
  newImagePrimary   = false;
  addingImage       = false;

  ngOnInit(): void {
    this.loadProducts();

    this.searchSubject.pipe(
      debounceTime(350),
      distinctUntilChanged(),
    ).subscribe(() => {
      this.pageIndex = 0;
      this.loadProducts();
    });
  }

  // ── Data ─────────────────────────────────────────────────────────────────

  loadProducts(): void {
    this.loading.set(true);
    this.service.list(this.pageIndex, this.pageSize, this.search).subscribe({
      next: (result) => {
        this.products.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to load products.' });
        this.loading.set(false);
      },
    });
  }

  onSearch(value: string): void {
    this.search = value;
    this.searchSubject.next(value);
  }

  onPageChange(event: { first: number; rows: number }): void {
    this.pageIndex = event.first / event.rows;
    this.pageSize  = event.rows;
    this.loadProducts();
  }

  // ── Create / Edit ─────────────────────────────────────────────────────────

  openCreate(): void {
    this.editingProduct.set(null);
    this.showFormDialog = true;
  }

  openEdit(product: ProductSummary): void {
    this.loading.set(true);
    this.service.getById(product.id).subscribe({
      next: (detail) => {
        this.editingProduct.set(detail);
        this.showFormDialog = true;
        this.loading.set(false);
      },
      error: () => {
        this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to load product.' });
        this.loading.set(false);
      },
    });
  }

  onFormSaved(): void {
    this.showFormDialog = false;
    this.editingProduct.set(null);
    this.toast.add({ severity: 'success', summary: 'Saved', detail: 'Product saved successfully.' });
    this.loadProducts();
  }

  onFormCancelled(): void {
    this.showFormDialog = false;
    this.editingProduct.set(null);
  }

  // ── Delete ────────────────────────────────────────────────────────────────

  confirmDelete(product: ProductSummary): void {
    this.confirmSvc.confirm({
      message: `Delete "${product.name}"? This cannot be undone.`,
      header:  'Confirm Delete',
      icon:    'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept:  () => this.deleteProduct(product.id),
    });
  }

  private deleteProduct(id: string): void {
    this.service.delete(id).subscribe({
      next: () => {
        this.toast.add({ severity: 'success', summary: 'Deleted', detail: 'Product removed.' });
        this.loadProducts();
      },
      error: () => {
        this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to delete product.' });
      },
    });
  }

  // ── Images Dialog ─────────────────────────────────────────────────────────

  openImages(product: ProductSummary): void {
    this.loading.set(true);
    this.service.getById(product.id).subscribe({
      next: (detail) => {
        this.imagesProduct.set(detail);
        this.showImagesDialog = true;
        this.loading.set(false);
      },
      error: () => {
        this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to load images.' });
        this.loading.set(false);
      },
    });
  }

  addImage(): void {
    const productId = this.imagesProduct()?.id;
    if (!productId || !this.newImageUrl.trim()) return;

    this.addingImage = true;
    const req: AddImageRequest = {
      url:       this.newImageUrl.trim(),
      altText:   this.newImageAlt.trim() || null,
      isPrimary: this.newImagePrimary,
    };

    this.service.addImage(productId, req).subscribe({
      next: () => {
        this.newImageUrl     = '';
        this.newImageAlt     = '';
        this.newImagePrimary = false;
        this.addingImage     = false;
        this.refreshImagesDialog(productId);
        this.toast.add({ severity: 'success', summary: 'Added', detail: 'Image added.' });
      },
      error: () => {
        this.addingImage = false;
        this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to add image.' });
      },
    });
  }

  removeImage(imageId: string): void {
    const productId = this.imagesProduct()?.id;
    if (!productId) return;

    this.service.removeImage(productId, imageId).subscribe({
      next: () => {
        this.refreshImagesDialog(productId);
        this.toast.add({ severity: 'success', summary: 'Removed', detail: 'Image removed.' });
      },
      error: () => {
        this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to remove image.' });
      },
    });
  }

  setPrimary(imageId: string): void {
    const productId = this.imagesProduct()?.id;
    if (!productId) return;

    this.service.setPrimaryImage(productId, imageId).subscribe({
      next: () => {
        this.refreshImagesDialog(productId);
        this.loadProducts();
      },
      error: () => {
        this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to set primary.' });
      },
    });
  }

  private refreshImagesDialog(productId: string): void {
    this.service.getById(productId).subscribe({
      next: (d) => {
        this.imagesProduct.set(d);
        // Update list thumbnail if visible
        this.loadProducts();
      },
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  stockSeverity(stock: number): 'success' | 'warn' | 'danger' {
    if (stock > 10) return 'success';
    if (stock > 0)  return 'warn';
    return 'danger';
  }

  stockLabel(stock: number): string {
    if (stock > 10) return `${stock} in stock`;
    if (stock > 0)  return `Low: ${stock}`;
    return 'Out of stock';
  }
}
