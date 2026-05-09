import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, finalize } from 'rxjs/operators';

import { LayoutService } from '../../../../layout/services/layout-service';
import { FilterStateService } from '../../../../shared/services/filter-state.service';
import {
  CreateOpenApplicationInputDto,
  OpenApplicationClientType,
  OpenApplicationOutputDto,
  OpenApplicationType,
  UpdateOpenApplicationInputDto
} from '../../models/open-application.dto';
import { OpenApplicationService } from '../../services/open-application-service';
import { OpenApplicationEditDialogComponent } from './widgets/open-application-edit-dialog/open-application-edit-dialog';
import { OpenApplicationTable, OpenApplicationTableFilterEvent } from './widgets/open-application-table/open-application-table';

@Component({
  selector: 'app-open-applications',
  imports: [
    CommonModule,
    FormsModule,
    SelectModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    ButtonModule,
    TooltipModule,
    ConfirmDialogModule,
    DialogModule,
    OpenApplicationTable,
    OpenApplicationEditDialogComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './open-applications.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OpenApplicationsPage implements OnInit {
  private readonly service = inject(OpenApplicationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);
  private readonly layoutService = inject(LayoutService);
  private readonly filterStateService = inject(FilterStateService);

  private readonly FILTER_KEY = 'open-applications';
  private readonly searchSubject = new Subject<string>();

  applications = signal<OpenApplicationOutputDto[]>([]);
  totalRecords = signal(0);
  loading = signal(false);

  editDialogVisible = signal(false);
  editDialogLoading = signal(false);
  editDialogSaving = signal(false);
  selectedApplication = signal<OpenApplicationOutputDto | null>(null);

  resetSecretDialogVisible = signal(false);
  resetSecretValue = signal('');

  searchQuery = signal('');
  selectedApplicationType = signal<OpenApplicationType | null>(null);
  selectedClientType = signal<OpenApplicationClientType | null>(null);

  offset = signal(0);
  limit = signal(10);
  sorting = signal('clientId asc');

  applicationTypeOptions = [
    { label: 'Web', value: 'web' },
    { label: '桌面/原生', value: 'native' },
    { label: '服务端', value: 'service' }
  ];

  clientTypeOptions = [
    { label: 'Public', value: 'public' },
    { label: 'Confidential', value: 'confidential' }
  ];

  constructor() {
    this.searchSubject
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.onFilter());
  }

  ngOnInit() {
    this.layoutService.title.set('开放应用管理');

    const saved = this.filterStateService.load<{
      searchQuery: string;
      selectedApplicationType: OpenApplicationType | null;
      selectedClientType: OpenApplicationClientType | null;
    }>(this.FILTER_KEY);

    if (saved.searchQuery) this.searchQuery.set(saved.searchQuery);
    if (saved.selectedApplicationType !== undefined) this.selectedApplicationType.set(saved.selectedApplicationType ?? null);
    if (saved.selectedClientType !== undefined) this.selectedClientType.set(saved.selectedClientType ?? null);

    this.reloadList();
  }

  reloadList() {
    this.loading.set(true);
    this.service
      .getOpenApplications({
        keyword: this.searchQuery(),
        applicationType: this.selectedApplicationType() ?? undefined,
        clientType: this.selectedClientType() ?? undefined,
        offset: this.offset(),
        limit: this.limit(),
        sorting: this.sorting()
      })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false))
      )
      .subscribe(data => {
        this.applications.set(data.items);
        this.totalRecords.set(data.totalCount);
      });
  }

  onSearchQueryChange(value: string) {
    this.searchQuery.set(value);
    this.searchSubject.next(value);
  }

  onApplicationTypeChange(value: OpenApplicationType | null) {
    this.selectedApplicationType.set(value);
    this.onFilter();
  }

  onClientTypeChange(value: OpenApplicationClientType | null) {
    this.selectedClientType.set(value);
    this.onFilter();
  }

  onFilter() {
    this.offset.set(0);
    this.filterStateService.save(this.FILTER_KEY, {
      searchQuery: this.searchQuery(),
      selectedApplicationType: this.selectedApplicationType(),
      selectedClientType: this.selectedClientType()
    });
    this.reloadList();
  }

  onPageChange(event: OpenApplicationTableFilterEvent) {
    this.offset.set(event.offset);
    this.limit.set(event.limit);
    if (event.sorting) this.sorting.set(event.sorting);
    this.reloadList();
  }

  openAddDialog() {
    this.selectedApplication.set(null);
    this.editDialogVisible.set(true);
  }

  openEditDialog(id: string) {
    this.selectedApplication.set(null);
    this.editDialogVisible.set(true);
    this.editDialogLoading.set(true);

    this.service
      .getOpenApplication(id)
      .pipe(finalize(() => this.editDialogLoading.set(false)))
      .subscribe(application => this.selectedApplication.set(application));
  }

  handleSave(data: CreateOpenApplicationInputDto | UpdateOpenApplicationInputDto) {
    this.editDialogSaving.set(true);
    const selected = this.selectedApplication();

    const request = selected
      ? this.service.updateOpenApplication(selected.id, data as UpdateOpenApplicationInputDto)
      : this.service.createOpenApplication(data as CreateOpenApplicationInputDto);

    request
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.editDialogSaving.set(false))
      )
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: '成功',
            detail: selected ? '开放应用更新成功' : '开放应用创建成功'
          });
          this.editDialogVisible.set(false);
          this.reloadList();
        }
      });
  }

  handleDelete(id: string) {
    this.confirmationService.confirm({
      message: '确定要删除此开放应用吗？使用该 Client ID 的客户端将无法继续登录。',
      header: '确认删除',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.service.deleteOpenApplication(id).subscribe(() => {
          this.messageService.add({ severity: 'success', summary: '成功', detail: '开放应用已删除' });
          this.reloadList();
        });
      }
    });
  }

  handleResetSecret(id: string) {
    this.confirmationService.confirm({
      message: '确定要重置该开放应用的 Client Secret 吗？旧密钥将立即失效。',
      header: '确认重置密钥',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.service.resetSecret(id).subscribe(result => {
          this.resetSecretValue.set(result.clientSecret);
          this.resetSecretDialogVisible.set(true);
          this.reloadList();
        });
      }
    });
  }

  copyResetSecret() {
    const value = this.resetSecretValue();
    if (!value) {
      return;
    }

    navigator.clipboard?.writeText(value).then(() => {
      this.messageService.add({ severity: 'success', summary: '成功', detail: '已复制密钥' });
    });
  }
}
