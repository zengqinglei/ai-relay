import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, input, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmPopupModule } from 'primeng/confirmpopup';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';

import { PlatformLabelPipe } from '../../../../../../shared/pipes/platform-label-pipe';
import { ApiKeyOutputDto, ApiKeyBindingOutputDto } from '../../../../models/subscription.dto';

export interface SubscriptionTableFilterEvent {
  offset: number;
  limit: number;
  sorting?: string;
}

@Component({
  selector: 'app-subscription-table',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    TagModule,
    TooltipModule,
    ToggleSwitchModule,
    ConfirmPopupModule,
    DialogModule,
    DatePickerModule,
    PlatformLabelPipe
  ],
  templateUrl: './subscription-table.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ConfirmationService]
})
export class SubscriptionTable {
  subscriptions = input.required<ApiKeyOutputDto[]>();
  totalRecords = input.required<number>();
  loading = input<boolean>(false);

  @Output() readonly edit = new EventEmitter<string>();
  @Output() readonly delete = new EventEmitter<string>();
  @Output() readonly statusToggle = new EventEmitter<{ id: string; isActive: boolean }>();
  @Output() readonly updateExpiryAndEnable = new EventEmitter<{ id: string; date: Date }>();
  @Output() readonly filterChange = new EventEmitter<SubscriptionTableFilterEvent>();

  private confirmationService = inject(ConfirmationService);
  private messageService = inject(MessageService);

  // Expiry Dialog State
  expiryDialogVisible = signal(false);
  selectedItemForExpiry = signal<ApiKeyOutputDto | null>(null);
  newExpiryDate = signal<Date | null>(null);

  // Secret visibility state (key: item.id, value: isVisible)
  secretVisibility: Record<string, boolean> = {};

  // Copy icon success state (key: item.id, value: isSuccess)
  copySuccess = signal<Record<string, boolean>>({});

  now = new Date();

  // Pagination state
  first = 0;
  rows = 10;
  sortField = signal<string>('creationTime');
  sortOrder = signal<number>(-1);

  onPage(event: TableLazyLoadEvent) {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 10;
    if (event.sortField) {
      this.sortField.set(Array.isArray(event.sortField) ? event.sortField[0] : event.sortField);
      this.sortOrder.set(event.sortOrder ?? -1);
    }
    this.filterChange.emit({
      offset: this.first,
      limit: this.rows,
      sorting: `${this.sortField()} ${this.sortOrder() === 1 ? 'asc' : 'desc'}`
    });
  }

  getBindingsTooltip(bindings: ApiKeyBindingOutputDto[]): string {
    if (!bindings || bindings.length === 0) return '';
    return bindings.map(b => `${b.providerGroupName} (${b.platform})`).join('\n');
  }

  private writeToClipboard(text: string): Promise<void> {
    if (navigator.clipboard?.writeText) {
      return navigator.clipboard.writeText(text);
    }
    // Fallback for non-secure contexts
    const textarea = document.createElement('textarea');
    textarea.value = text;
    textarea.style.position = 'fixed';
    textarea.style.opacity = '0';
    document.body.appendChild(textarea);
    textarea.focus();
    textarea.select();
    document.execCommand('copy');
    document.body.removeChild(textarea);
    return Promise.resolve();
  }

  copyKey(key: string) {
    this.writeToClipboard(key).then(() => {
      this.messageService.add({ severity: 'success', summary: '成功', detail: '已复制' });
    });
  }

  toggleSecretVisibility(itemId: string) {
    this.secretVisibility[itemId] = !this.secretVisibility[itemId];
  }

  getMaskedSecret(secret: string): string {
    if (!secret || secret.length < 12) return '***';
    return `${secret.substring(0, 7)}...${secret.substring(secret.length - 4)}`;
  }

  getDisplaySecret(item: ApiKeyOutputDto): string {
    if (this.secretVisibility[item.id]) {
      return item.secret;
    }
    return this.getMaskedSecret(item.secret);
  }

  copySecret(item: ApiKeyOutputDto) {
    this.writeToClipboard(item.secret).then(() => {
      this.messageService.add({ severity: 'success', summary: '成功', detail: '已复制密钥', life: 2000 });
      this.copySuccess.update(state => ({ ...state, [item.id]: true }));
      setTimeout(() => {
        this.copySuccess.update(state => ({ ...state, [item.id]: false }));
      }, 2000);
    });
  }

  getExpiryState(item: ApiKeyOutputDto): 'expired' | 'warning' | 'ok' | 'forever' {
    if (!item.expiresAt) return 'forever';

    const now = new Date().getTime();
    const expiry = new Date(item.expiresAt).getTime();
    const diff = expiry - now;

    if (diff <= 0) return 'expired';
    if (diff < 3 * 24 * 3600 * 1000) return 'warning'; // Warning if < 3 days
    return 'ok';
  }

  isEffectiveActive(item: ApiKeyOutputDto): boolean {
    return item.isActive && this.getExpiryState(item) !== 'expired';
  }

  confirmStatusToggle(event: Event, item: ApiKeyOutputDto) {
    // 1. Check if expired
    if (this.getExpiryState(item) === 'expired') {
      this.openExpiryDialog(item);
      return;
    }

    // 2. Normal toggle
    this.confirmationService.confirm({
      target: event.target as EventTarget,
      message: `确定要${item.isActive ? '禁用' : '启用'}该订阅吗？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '确定',
      rejectLabel: '取消',
      accept: () => {
        this.statusToggle.emit({ id: item.id, isActive: !item.isActive });
      }
    });
  }

  openExpiryDialog(item: ApiKeyOutputDto) {
    this.selectedItemForExpiry.set(item);
    // Default to 1 month from now
    const date = new Date();
    date.setMonth(date.getMonth() + 1);
    this.newExpiryDate.set(date);
    this.expiryDialogVisible.set(true);
  }

  saveExpiry() {
    const item = this.selectedItemForExpiry();
    const date = this.newExpiryDate();
    if (item && date) {
      // Validate date > now
      if (date.getTime() <= new Date().getTime()) {
        // Should show error, but for now just ignore or rely on minDate if set
        return;
      }
      this.updateExpiryAndEnable.emit({ id: item.id, date: date });
      this.expiryDialogVisible.set(false);
    }
  }
}
