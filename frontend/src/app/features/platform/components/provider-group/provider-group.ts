import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { finalize } from 'rxjs/operators';


import {
  GetProviderGroupsInputDto,
  CreateProviderGroupInputDto,
  UpdateProviderGroupInputDto,
  ProviderGroupOutputDto
} from '../../models/provider-group.dto';
import { ProviderGroupService } from '../../services/provider-group-service';
import { GroupEditDialogComponent } from './widgets/group-edit-dialog/group-edit-dialog';
import { GroupTable, GroupTableFilterEvent } from './widgets/group-table/group-table';
import { LayoutService } from '../../../../layout/services/layout-service';

@Component({
  selector: 'app-provider-group',
  standalone: true,
  imports: [CommonModule, GroupTable, GroupEditDialogComponent, ConfirmDialogModule],
  providers: [ConfirmationService],
  templateUrl: './provider-group.html'
})
export class ProviderGroupPage implements OnInit {
  private service = inject(ProviderGroupService);
  private destroyRef = inject(DestroyRef);
  private confirmationService = inject(ConfirmationService);
  private messageService = inject(MessageService);
  private layoutService = inject(LayoutService);

  // State
  groups = signal<ProviderGroupOutputDto[]>([]);
  totalRecords = signal(0);
  loading = signal(false);

  // Dialog State
  editDialogVisible = signal(false);
  editDialogLoading = signal(false); // Loading for fetching group details
  editDialogSaving = signal(false); // Loading for saving operation
  selectedGroup = signal<ProviderGroupOutputDto | null>(null);

  // Filter State
  currentFilter = signal<GetProviderGroupsInputDto>({
    offset: 0,
    limit: 10
  });

  ngOnInit() {
    this.layoutService.title.set('资源池');
    // Initial load will be triggered by table's onLazyLoad or we trigger manually if lazy load doesn't fire on init (PrimeNG usually fires it)
    // For safety, we can trigger it or wait for table.
    // If table [lazy]="true", it usually triggers onInit.
    // Let's rely on Table to trigger the first load via onPage/onLazyLoad if we bound it correctly.
    // However, in GroupTable, we emit filterChange onPage.
    // But we need to listen to filterChange in the parent template.
  }

  loadData() {
    this.loading.set(true);
    this.service
      .getGroups(this.currentFilter())
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false))
      )
      .subscribe(result => {
        this.groups.set(result.items);
        this.totalRecords.set(result.totalCount);
      });
  }

  onFilterChange(event: GroupTableFilterEvent) {
    const filter: GetProviderGroupsInputDto = {
      offset: event.offset,
      limit: event.limit,
      keyword: event.q,
      sorting: event.sorting
    };
    this.currentFilter.set(filter);
    this.loadData();
  }

  // --- Actions ---

  openAddDialog() {
    this.selectedGroup.set(null);
    this.editDialogVisible.set(true);
  }

  openEditDialog(id: string) {
    // Open dialog immediately for better UX
    this.selectedGroup.set(null); // Clear first to avoid showing stale data
    this.editDialogVisible.set(true);
    this.editDialogLoading.set(true);

    // Fetch detail in background
    this.service
      .getGroup(id)
      .pipe(finalize(() => this.editDialogLoading.set(false)))
      .subscribe(group => {
        this.selectedGroup.set(group);
      });
  }

  handleSave(dto: CreateProviderGroupInputDto | UpdateProviderGroupInputDto) {
    this.editDialogSaving.set(true);

    if (this.selectedGroup()) {
      this.service
        .updateGroup(this.selectedGroup()!.id, dto)
        .pipe(
          takeUntilDestroyed(this.destroyRef),
          finalize(() => this.editDialogSaving.set(false))
        )
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: '成功', detail: '分组更新成功' });
            this.editDialogVisible.set(false);
            this.loadData();
          }
        });
    } else {
      this.service
        .createGroup(dto)
        .pipe(
          takeUntilDestroyed(this.destroyRef),
          finalize(() => this.editDialogSaving.set(false))
        )
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: '成功', detail: '分组创建成功' });
            this.editDialogVisible.set(false);
            this.loadData();
          }
        });
    }
  }

  handleDelete(id: string) {
    this.confirmationService.confirm({
      message: '确定要删除该分组吗？删除后关联的ApiKey可能无法正常工作。',
      header: '确认删除',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.service.deleteGroup(id).subscribe(() => {
          this.messageService.add({ severity: 'success', summary: '成功', detail: '分组已删除' });
          this.loadData();
        });
      }
    });
  }
}
