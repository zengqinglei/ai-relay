import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { finalize } from 'rxjs/operators';

import { LayoutService } from '../../../../layout/services/layout-service';
import {
  CreateProviderGroupInputDto,
  GetProviderGroupsInputDto,
  ProviderGroupOutputDto,
  UpdateProviderGroupInputDto
} from '../../models/provider-group.dto';
import { ProviderGroupService } from '../../services/provider-group-service';
import { GroupEditDialogComponent } from './widgets/group-edit-dialog/group-edit-dialog';
import { GroupTable, GroupTableFilterEvent } from './widgets/group-table/group-table';

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

  groups = signal<ProviderGroupOutputDto[]>([]);
  totalRecords = signal(0);
  loading = signal(false);

  editDialogVisible = signal(false);
  editDialogLoading = signal(false);
  editDialogSaving = signal(false);
  selectedGroup = signal<ProviderGroupOutputDto | null>(null);

  currentFilter = signal<GetProviderGroupsInputDto>({
    offset: 0,
    limit: 10
  });

  ngOnInit() {
    this.layoutService.title.set('资源池');
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
    this.currentFilter.set({
      offset: event.offset,
      limit: event.limit,
      keyword: event.q,
      sorting: event.sorting
    });
    this.loadData();
  }

  openAddDialog() {
    this.selectedGroup.set(null);
    this.editDialogVisible.set(true);
  }

  openEditDialog(id: string) {
    this.selectedGroup.set(null);
    this.editDialogVisible.set(true);
    this.editDialogLoading.set(true);

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
    const target = this.groups().find(group => group.id === id);
    if (target?.isDefault) {
      this.messageService.add({ severity: 'warn', summary: '提示', detail: '默认分组不可删除' });
      return;
    }

    this.confirmationService.confirm({
      message: '确定要删除该分组吗？删除后关联的 ApiKey 可能无法正常工作。',
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
