import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, finalize } from 'rxjs/operators';

import { LayoutService } from '../../../../layout/services/layout-service';
import { Role, ROLE_LABEL_MAP } from '../../../../shared/models/role.enum';
import { FilterStateService } from '../../../../shared/services/filter-state.service';
import {
  CreateUserInputDto,
  ResetUserPasswordInputDto,
  UpdateUserInputDto,
  UserManagementOutputDto
} from '../../models/user-management.dto';
import { UserManagementService } from '../../services/user-management-service';
import { ResetUserPasswordDialogComponent } from './widgets/reset-user-password-dialog/reset-user-password-dialog';
import { UserEditDialogComponent } from './widgets/user-edit-dialog/user-edit-dialog';
import { UserTable, UserTableFilterEvent } from './widgets/user-table/user-table';

@Component({
  selector: 'app-users',
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
    UserTable,
    UserEditDialogComponent,
    ResetUserPasswordDialogComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './users.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UsersPage implements OnInit {
  private readonly service = inject(UserManagementService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);
  private readonly layoutService = inject(LayoutService);
  private readonly filterStateService = inject(FilterStateService);

  private readonly FILTER_KEY = 'users';
  private readonly searchSubject = new Subject<string>();

  users = signal<UserManagementOutputDto[]>([]);
  totalRecords = signal(0);
  loading = signal(false);

  editDialogVisible = signal(false);
  editDialogLoading = signal(false);
  editDialogSaving = signal(false);
  selectedUser = signal<UserManagementOutputDto | null>(null);

  resetPasswordDialogVisible = signal(false);
  resetPasswordSaving = signal(false);
  resettingUserId = signal<string | null>(null);

  searchQuery = signal('');
  selectedIsActive = signal<boolean | null>(null);
  selectedIsEmailVerified = signal<boolean | null>(null);
  selectedRole = signal<string | null>(null);

  offset = signal(0);
  limit = signal(10);
  sorting = signal('username asc');

  activeOptions = [
    { label: '启用', value: true },
    { label: '禁用', value: false }
  ];

  emailVerifiedOptions = [
    { label: '已验证', value: true },
    { label: '未验证', value: false }
  ];

  roleOptions = Object.entries(ROLE_LABEL_MAP).map(([value, label]) => ({ label, value }));

  constructor() {
    this.searchSubject
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.onFilter());
  }

  ngOnInit() {
    this.layoutService.title.set('用户管理');

    const saved = this.filterStateService.load<{
      searchQuery: string;
      selectedIsActive: boolean | null;
      selectedIsEmailVerified: boolean | null;
      selectedRole: string | null;
    }>(this.FILTER_KEY);

    if (saved.searchQuery) this.searchQuery.set(saved.searchQuery);
    if (saved.selectedIsActive !== undefined) this.selectedIsActive.set(saved.selectedIsActive ?? null);
    if (saved.selectedIsEmailVerified !== undefined) this.selectedIsEmailVerified.set(saved.selectedIsEmailVerified ?? null);
    if (saved.selectedRole !== undefined) this.selectedRole.set(saved.selectedRole ?? null);
  }

  reloadList() {
    this.loading.set(true);
    this.service
      .getUsers({
        keyword: this.searchQuery(),
        isActive: this.selectedIsActive() ?? undefined,
        isEmailVerified: this.selectedIsEmailVerified() ?? undefined,
        role: this.selectedRole() ?? undefined,
        offset: this.offset(),
        limit: this.limit(),
        sorting: this.sorting()
      })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false))
      )
      .subscribe(data => {
        this.users.set(data.items);
        this.totalRecords.set(data.totalCount);
      });
  }

  onSearchQueryChange(value: string) {
    this.searchQuery.set(value);
    this.searchSubject.next(value);
  }

  onActiveChange(value: boolean | null) {
    this.selectedIsActive.set(value);
    this.onFilter();
  }

  onEmailVerifiedChange(value: boolean | null) {
    this.selectedIsEmailVerified.set(value);
    this.onFilter();
  }

  onRoleChange(value: string | null) {
    this.selectedRole.set(value);
    this.onFilter();
  }

  onFilter() {
    this.offset.set(0);
    this.filterStateService.save(this.FILTER_KEY, {
      searchQuery: this.searchQuery(),
      selectedIsActive: this.selectedIsActive(),
      selectedIsEmailVerified: this.selectedIsEmailVerified(),
      selectedRole: this.selectedRole()
    });
    this.reloadList();
  }

  onPageChange(event: UserTableFilterEvent) {
    this.offset.set(event.offset);
    this.limit.set(event.limit);
    if (event.sorting) this.sorting.set(event.sorting);
    this.reloadList();
  }

  openAddDialog() {
    this.selectedUser.set(null);
    this.editDialogVisible.set(true);
  }

  openEditDialog(id: string) {
    this.selectedUser.set(null);
    this.editDialogVisible.set(true);
    this.editDialogLoading.set(true);

    this.service
      .getUser(id)
      .pipe(finalize(() => this.editDialogLoading.set(false)))
      .subscribe(user => this.selectedUser.set(user));
  }

  handleSave(data: CreateUserInputDto | UpdateUserInputDto) {
    this.editDialogSaving.set(true);
    const selected = this.selectedUser();
    const request = selected
      ? this.service.updateUser(selected.id, data as UpdateUserInputDto)
      : this.service.createUser(data as CreateUserInputDto);

    request
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.editDialogSaving.set(false))
      )
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: '成功', detail: selected ? '用户更新成功' : '用户创建成功' });
          this.editDialogVisible.set(false);
          this.reloadList();
        }
      });
  }

  handleToggleActive(user: UserManagementOutputDto) {
    this.confirmationService.confirm({
      message: user.isActive ? `确定要禁用用户 ${user.username} 吗？` : `确定要启用用户 ${user.username} 吗？`,
      header: user.isActive ? '确认禁用' : '确认启用',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        const request = user.isActive ? this.service.disableUser(user.id) : this.service.enableUser(user.id);
        request.subscribe(() => {
          this.messageService.add({ severity: 'success', summary: '成功', detail: user.isActive ? '用户已禁用' : '用户已启用' });
          this.reloadList();
        });
      }
    });
  }

  handleDelete(user: UserManagementOutputDto) {
    if (user.isSuperAdmin) {
      this.messageService.add({ severity: 'warn', summary: '无法删除', detail: '系统内置超级管理员不允许删除' });
      return;
    }

    this.confirmationService.confirm({
      message: `确定要删除用户 ${user.username} 吗？删除后该用户将无法继续登录。`,
      header: '确认删除',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.service.deleteUser(user.id).subscribe(() => {
          this.messageService.add({ severity: 'success', summary: '成功', detail: '用户已删除' });
          this.reloadList();
        });
      }
    });
  }

  openResetPasswordDialog(id: string) {
    this.resettingUserId.set(id);
    this.resetPasswordDialogVisible.set(true);
  }

  handleResetPassword(data: ResetUserPasswordInputDto) {
    const id = this.resettingUserId();
    if (!id) {
      return;
    }

    this.resetPasswordSaving.set(true);
    this.service
      .resetPassword(id, data)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.resetPasswordSaving.set(false))
      )
      .subscribe(() => {
        this.messageService.add({ severity: 'success', summary: '成功', detail: '密码已重置' });
        this.resetPasswordDialogVisible.set(false);
        this.resettingUserId.set(null);
      });
  }
}
