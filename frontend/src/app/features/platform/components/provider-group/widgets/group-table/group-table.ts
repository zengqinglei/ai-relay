import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, inject, input, OnInit, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { Popover, PopoverModule } from 'primeng/popover';
import { RippleModule } from 'primeng/ripple';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';

import { ROUTE_PROFILE_FULL_LABELS, ROUTE_PROFILE_LABELS } from '../../../../../../shared/constants/route-profile.constants';
import { RouteProfile } from '../../../../../../shared/models/route-profile.enum';
import { FilterStateService } from '../../../../../../shared/services/filter-state.service';
import { ProviderGroupOutputDto } from '../../../../models/provider-group.dto';
import {
  RelationPopoverContentComponent,
  RelationPopoverItem
} from '../../../shared/widgets/relation-popover-content/relation-popover-content';

export interface GroupTableFilterEvent {
  offset: number;
  limit: number;
  q?: string;
  sorting?: string;
}

@Component({
  selector: 'app-group-table',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    TagModule,
    TooltipModule,
    IconFieldModule,
    InputIconModule,
    RippleModule,
    PopoverModule,
    RelationPopoverContentComponent
  ],
  templateUrl: './group-table.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GroupTable implements OnInit {
  groups = input.required<ProviderGroupOutputDto[]>();
  totalRecords = input.required<number>();
  loading = input<boolean>(false);

  @Output() readonly filterChange = new EventEmitter<GroupTableFilterEvent>();
  @Output() readonly add = new EventEmitter<void>();
  @Output() readonly edit = new EventEmitter<string>();
  @Output() readonly delete = new EventEmitter<string>();

  searchQuery = signal('');
  first = 0;
  rows = 10;
  sortField = signal<string>('creationTime');
  sortOrder = signal<number>(-1);
  activeRouteProfiles = signal<RouteProfile[]>([]);
  visibleRouteCount = signal(2);

  private readonly filterStateService = inject(FilterStateService);
  private readonly searchSubject = new Subject<string>();
  private readonly FILTER_KEY = 'provider-group';

  constructor() {
    this.searchSubject
      .pipe(debounceTime(300), distinctUntilChanged())
      .subscribe(() => this.onFilter());
  }

  ngOnInit() {
    const saved = this.filterStateService.load<{ keyword: string }>(this.FILTER_KEY);
    if (saved.keyword) {
      this.searchQuery.set(saved.keyword);
    }
  }

  onSearchQueryChange(value: string) {
    this.searchQuery.set(value);
    this.searchSubject.next(value);
  }

  onFilter() {
    this.first = 0;
    this.emitFilterChange();
  }

  onPage(event: TableLazyLoadEvent) {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 10;
    if (event.sortField) {
      this.sortField.set(Array.isArray(event.sortField) ? event.sortField[0] : event.sortField);
      this.sortOrder.set(event.sortOrder ?? -1);
    }
    this.emitFilterChange();
  }

  private emitFilterChange() {
    this.filterStateService.save(this.FILTER_KEY, {
      keyword: this.searchQuery()
    });
    this.filterChange.emit({
      offset: this.first,
      limit: this.rows,
      q: this.searchQuery(),
      sorting: `${this.sortField()} ${this.sortOrder() === 1 ? 'asc' : 'desc'}`
    });
  }

  isDefaultGroup(group: ProviderGroupOutputDto): boolean {
    return group.isDefault === true;
  }

  toggleRouteProfilesPopover(event: Event, popover: Popover, profiles: RouteProfile[]) {
    this.activeRouteProfiles.set(profiles);
    popover.toggle(event);
  }

  getRoutePopoverItems(profiles: RouteProfile[]): RelationPopoverItem[] {
    return profiles.map(profile => ({
      id: profile,
      leftText: this.getRouteProfileLabel(profile),
      rightText: this.getRouteProfilePath(profile)
    }));
  }

  getRouteProfilePath(profile: RouteProfile): string {
    const fullLabel = this.getRouteProfileFullLabel(profile);
    const match = /\(([^)]+)\)$/.exec(fullLabel);
    return match?.[1] ?? fullLabel;
  }

  getRouteProfileLabel(profile: RouteProfile): string {
    return ROUTE_PROFILE_LABELS[profile] || profile;
  }

  getRouteProfileFullLabel(profile: RouteProfile): string {
    return ROUTE_PROFILE_FULL_LABELS[profile] || profile;
  }
}
