import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { Popover, PopoverModule } from 'primeng/popover';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';

import { OpenApplicationOutputDto } from '../../../../models/open-application.dto';

export interface OpenApplicationTableFilterEvent {
  offset: number;
  limit: number;
  sorting?: string;
}

type PopoverMode = 'permissions' | 'redirectUris';

@Component({
  selector: 'app-open-application-table',
  imports: [CommonModule, TableModule, ButtonModule, TagModule, TooltipModule, PopoverModule],
  templateUrl: './open-application-table.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OpenApplicationTable {
  applications = input.required<OpenApplicationOutputDto[]>();
  totalRecords = input.required<number>();
  loading = input<boolean>(false);

  readonly edit = output<string>();
  readonly delete = output<string>();
  readonly resetSecret = output<string>();
  readonly filterChange = output<OpenApplicationTableFilterEvent>();

  first = 0;
  rows = 10;
  sortField = signal('clientId');
  sortOrder = signal(1);
  activeItems = signal<string[]>([]);
  popoverMode = signal<PopoverMode>('permissions');

  popoverTitle = computed(() => {
    switch (this.popoverMode()) {
      case 'redirectUris':
        return 'Redirect URIs';
      default:
        return '授权能力';
    }
  });

  onPage(event: TableLazyLoadEvent) {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 10;
    if (event.sortField) {
      this.sortField.set(Array.isArray(event.sortField) ? event.sortField[0] : event.sortField);
      this.sortOrder.set(event.sortOrder ?? 1);
    }
    this.filterChange.emit({
      offset: this.first,
      limit: this.rows,
      sorting: `${this.sortField()} ${this.sortOrder() === 1 ? 'asc' : 'desc'}`
    });
  }

  openPopover(event: Event, popover: Popover, mode: PopoverMode, items: string[]) {
    this.popoverMode.set(mode);
    this.activeItems.set(items);
    popover.toggle(event);
  }

  getApplicationTypeLabel(value: string) {
    const labels: Record<string, string> = {
      web: 'Web',
      native: '桌面/原生',
      service: '服务端'
    };
    return labels[value] ?? value;
  }

  getClientTypeSeverity(value: string): 'success' | 'info' | 'warn' | 'secondary' {
    return value === 'public' ? 'success' : 'warn';
  }

  getConsentTypeLabel(value: string) {
    const labels: Record<string, string> = {
      implicit: '隐式同意',
      explicit: '显式同意',
      external: '外部同意',
      systematic: '系统同意'
    };
    return labels[value] ?? value;
  }

  getPermissionSummary(item: OpenApplicationOutputDto) {
    const grants = item.permissions.filter(permission => permission.startsWith('gt:')).map(permission => permission.replace('gt:', ''));
    return grants.length ? grants.join(' / ') : '未配置';
  }

  getVisibleRedirectUris(item: OpenApplicationOutputDto) {
    return item.redirectUris.slice(0, 1);
  }

  getHiddenRedirectUris(item: OpenApplicationOutputDto) {
    return item.redirectUris.slice(1);
  }

  hasPkce(item: OpenApplicationOutputDto) {
    return item.requirements.includes('ft:pkce');
  }
}
