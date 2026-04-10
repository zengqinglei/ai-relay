import { CommonModule } from '@angular/common';
import { AfterViewInit, ChangeDetectionStrategy, Component, DestroyRef, ElementRef, EventEmitter, ViewChild, input, Output, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmPopupModule } from 'primeng/confirmpopup';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { Popover, PopoverModule } from 'primeng/popover';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';

import { LayoutService } from '../../../../../../layout/services/layout-service';
import { ROUTE_PROFILE_FULL_LABELS, ROUTE_PROFILE_LABELS } from '../../../../../../shared/constants/route-profile.constants';
import { RouteProfile } from '../../../../../../shared/models/route-profile.enum';
import { formatTokenCount } from '../../../../../../shared/utils/format.utils';
import { ApiKeyBindingOutputDto, ApiKeyOutputDto } from '../../../../models/subscription.dto';
import {
  RelationPopoverContentComponent,
  RelationPopoverItem
} from '../../../shared/widgets/relation-popover-content/relation-popover-content';

type BindPopoverMode = 'details' | 'summary';

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
    PopoverModule,
    RelationPopoverContentComponent
  ],
  templateUrl: './subscription-table.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ConfirmationService]
})
export class SubscriptionTable implements AfterViewInit {
  subscriptions = input.required<ApiKeyOutputDto[]>();
  totalRecords = input.required<number>();
  loading = input<boolean>(false);

  @Output() readonly edit = new EventEmitter<string>();
  @Output() readonly delete = new EventEmitter<string>();
  @Output() readonly statusToggle = new EventEmitter<{ id: string; isActive: boolean }>();
  @Output() readonly updateExpiryAndEnable = new EventEmitter<{ id: string; date: Date }>();
  @Output() readonly filterChange = new EventEmitter<SubscriptionTableFilterEvent>();

  @ViewChild('bindingColumnContainer') bindingColumnContainer?: ElementRef<HTMLElement>;

  private confirmationService = inject(ConfirmationService);
  private messageService = inject(MessageService);
  private readonly layoutService = inject(LayoutService);
  private readonly destroyRef = inject(DestroyRef);

  expiryDialogVisible = signal(false);
  selectedItemForExpiry = signal<ApiKeyOutputDto | null>(null);
  newExpiryDate = signal<Date | null>(null);
  secretVisibility: Record<string, boolean> = {};
  copySuccess = signal<Record<string, boolean>>({});
  now = new Date();

  first = 0;
  rows = 10;
  sortField = signal<string>('creationTime');
  sortOrder = signal<number>(-1);
  activeBindings = signal<ApiKeyBindingOutputDto[]>([]);
  bindPopoverMode = signal<BindPopoverMode>('summary');
  visibleBindingCount = signal(3);
  private bindingResizeObserver?: ResizeObserver;

  ngAfterViewInit(): void {
    queueMicrotask(() => this.setupBindingResizeObserver());
  }

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

  private setupBindingResizeObserver(): void {
    const element = this.bindingColumnContainer?.nativeElement;
    if (!element || typeof ResizeObserver === 'undefined') {
      this.updateVisibleBindingCount(this.layoutService.sidebarCollapsed() ? 18 * 16 : 15 * 16);
      return;
    }

    this.bindingResizeObserver?.disconnect();
    this.bindingResizeObserver = new ResizeObserver(entries => {
      const width = entries[0]?.contentRect.width ?? element.clientWidth;
      this.updateVisibleBindingCount(width);
    });
    this.bindingResizeObserver.observe(element);
    this.destroyRef.onDestroy(() => this.bindingResizeObserver?.disconnect());
  }

  private updateVisibleBindingCount(width: number): void {
    if (width >= 280) {
      this.visibleBindingCount.set(3);
      return;
    }

    if (width >= 190) {
      this.visibleBindingCount.set(2);
      return;
    }

    this.visibleBindingCount.set(1);
  }

  getVisibleBindings(item: ApiKeyOutputDto): ApiKeyBindingOutputDto[] {
    return item.bindings?.slice(0, this.visibleBindingCount()) ?? [];
  }

  getHiddenBindings(item: ApiKeyOutputDto): ApiKeyBindingOutputDto[] {
    return item.bindings?.slice(this.visibleBindingCount()) ?? [];
  }

  openBindingDetailsPopover(event: Event, popover: Popover, binding: ApiKeyBindingOutputDto) {
    this.bindPopoverMode.set('details');
    this.activeBindings.set([binding]);
    popover.toggle(event);
  }

  openBindingSummaryPopover(event: Event, popover: Popover, bindings: ApiKeyBindingOutputDto[]) {
    this.bindPopoverMode.set('summary');
    this.activeBindings.set(bindings);
    popover.toggle(event);
  }

  getBindingPopoverItems(): RelationPopoverItem[] {
    if (this.bindPopoverMode() === 'details') {
      return this.activeBindings().flatMap(binding => {
        if (!binding.supportedRouteProfiles?.length) {
          return [
            {
              id: `${binding.providerGroupId}-empty`,
              leftText: binding.providerGroupName || '未知分组',
              rightText: '空资源池',
              isWarning: true
            }
          ];
        }

        return binding.supportedRouteProfiles.map(profile => ({
          id: `${binding.providerGroupId}-${profile}`,
          leftText: this.getRouteProfileLabel(profile),
          rightText: this.getRouteProfilePath(profile)
        }));
      });
    }

    return this.activeBindings().map(binding => ({
      id: binding.providerGroupId,
      leftText: binding.providerGroupName || '未知分组',
      rightText: this.getBindingRouteBadgeSummary(binding),
      isWarning: !binding.supportedRouteProfiles?.length
    }));
  }

  private writeToClipboard(text: string): Promise<void> {
    if (navigator.clipboard?.writeText) {
      return navigator.clipboard.writeText(text);
    }
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

  getRouteProfilePath(profile: RouteProfile): string {
    const fullLabel = this.getRouteProfileFullLabel(profile);
    const match = /\(([^)]+)\)$/.exec(fullLabel);
    return match?.[1] ?? fullLabel;
  }

  getBindingRouteBadgeSummary(binding: ApiKeyBindingOutputDto): string {
    if (!binding.supportedRouteProfiles?.length) {
      return '空资源池';
    }

    return binding.supportedRouteProfiles.map(profile => this.getRouteProfileLabel(profile)).join(' | ');
  }

  getRouteProfileLabel(profile: RouteProfile): string {
    return ROUTE_PROFILE_LABELS[profile] || profile;
  }

  getRouteProfileFullLabel(profile: RouteProfile): string {
    return ROUTE_PROFILE_FULL_LABELS[profile] || profile;
  }

  getExpiryState(item: ApiKeyOutputDto): 'expired' | 'warning' | 'ok' | 'forever' {
    if (!item.expiresAt) return 'forever';

    const now = new Date().getTime();
    const expiry = new Date(item.expiresAt).getTime();
    const diff = expiry - now;

    if (diff <= 0) return 'expired';
    if (diff < 3 * 24 * 3600 * 1000) return 'warning';
    return 'ok';
  }

  isEffectiveActive(item: ApiKeyOutputDto): boolean {
    return item.isActive && this.getExpiryState(item) !== 'expired';
  }

  confirmStatusToggle(event: Event, item: ApiKeyOutputDto) {
    if (this.getExpiryState(item) === 'expired') {
      this.openExpiryDialog(item);
      return;
    }

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
    const date = new Date();
    date.setMonth(date.getMonth() + 1);
    this.newExpiryDate.set(date);
    this.expiryDialogVisible.set(true);
  }

  saveExpiry() {
    const item = this.selectedItemForExpiry();
    const date = this.newExpiryDate();
    if (item && date) {
      if (date.getTime() <= new Date().getTime()) {
        return;
      }
      this.updateExpiryAndEnable.emit({ id: item.id, date: date });
      this.expiryDialogVisible.set(false);
    }
  }

  formatTokenCount = formatTokenCount;
}
