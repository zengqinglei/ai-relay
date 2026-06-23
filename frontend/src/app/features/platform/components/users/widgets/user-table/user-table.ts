import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { AvatarModule } from 'primeng/avatar';
import { ButtonModule } from 'primeng/button';
import { Popover, PopoverModule } from 'primeng/popover';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';

import { AuthService } from '../../../../../../core/services/auth-service';
import { Role } from '../../../../../../shared/models/role.enum';
import { getRoleLabel } from '../../../../../../shared/pipes/role-label.pipe';
import { UserManagementOutputDto } from '../../../../models/user-management.dto';

export interface UserTableFilterEvent {
  offset: number;
  limit: number;
  sorting?: string;
}

@Component({
  selector: 'app-user-table',
  imports: [CommonModule, TableModule, ButtonModule, TagModule, TooltipModule, PopoverModule, AvatarModule],
  templateUrl: './user-table.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserTable {
  private readonly authService = inject(AuthService);

  users = input.required<UserManagementOutputDto[]>();
  totalRecords = input.required<number>();
  loading = input<boolean>(false);

  readonly edit = output<string>();
  readonly toggleActive = output<UserManagementOutputDto>();
  readonly resetPassword = output<string>();
  readonly delete = output<UserManagementOutputDto>();
  readonly filterChange = output<UserTableFilterEvent>();

  first = 0;
  rows = 20;
  sortField = signal('username');
  sortOrder = signal(1);
  activeRoles = signal<string[]>([]);
  rolePopoverTitle = computed(() => `角色 (${this.activeRoles().length})`);

  onPage(event: TableLazyLoadEvent) {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 20;
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

  openRolesPopover(event: Event, popover: Popover, roles: string[]) {
    this.activeRoles.set(roles);
    popover.toggle(event);
  }

  getVisibleRoles(user: UserManagementOutputDto) {
    return user.roles.slice(0, 2);
  }

  getHiddenRoles(user: UserManagementOutputDto) {
    return user.roles.slice(2);
  }

  isSuperAdmin(user: UserManagementOutputDto) {
    return user.isSuperAdmin;
  }

  isOtherSuperAdmin(user: UserManagementOutputDto) {
    return user.isSuperAdmin && user.id !== this.authService.currentUser()?.id;
  }

  isSelfSuperAdmin(user: UserManagementOutputDto) {
    return user.isSuperAdmin && user.id === this.authService.currentUser()?.id;
  }

  getRoleSeverity(role: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    const roleMap: Record<Role, 'success' | 'info' | 'warn' | 'danger' | 'secondary'> = {
      [Role.Admin]: 'danger',
      [Role.Operator]: 'warn',
      [Role.Member]: 'info'
    };
    return roleMap[role as Role] ?? 'secondary';
  }

  getRoleLabel = getRoleLabel;
}
