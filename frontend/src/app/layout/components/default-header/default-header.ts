import { ChangeDetectionStrategy, Component, inject, signal, output, ViewChild, OnDestroy } from '@angular/core';
import { RouterModule, Router } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { AvatarModule } from 'primeng/avatar';
import { BadgeModule } from 'primeng/badge';
import { ButtonModule } from 'primeng/button';
import { MenuModule, Menu } from 'primeng/menu';
import { StyleClassModule } from 'primeng/styleclass';
import { TooltipModule } from 'primeng/tooltip';

import { AuthService } from '../../../core/services/auth-service';
import { ChangePasswordDialogComponent } from '../../../features/account/components/change-password-dialog/change-password-dialog';
import { ProfileSettingsDialogComponent } from '../../../features/account/components/profile-settings-dialog/profile-settings-dialog';
import { ThemeConfigurator } from '../../../shared/components/theme-configurator/theme-configurator';
import { LayoutService } from '../../services/layout-service';

@Component({
  selector: 'app-default-header',
  standalone: true,
  imports: [
    RouterModule,
    ButtonModule,
    AvatarModule,
    BadgeModule,
    StyleClassModule,
    TooltipModule,
    MenuModule,
    ThemeConfigurator,
    ProfileSettingsDialogComponent,
    ChangePasswordDialogComponent
  ],
  templateUrl: './default-header.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DefaultHeader implements OnDestroy {
  readonly layoutService = inject(LayoutService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  @ViewChild('userMenu') userMenu!: Menu;

  readonly toggleMobileMenu = output<void>();
  readonly notificationCount = signal(1);
  readonly profileDialogVisible = signal(false);
  readonly changePasswordDialogVisible = signal(false);

  userMenuItems: MenuItem[] = [
    {
      label: '工作空间',
      icon: 'pi pi-home',
      command: () => this.closeMenuAndNavigate('/workspace')
    },
    {
      label: '设置',
      icon: 'pi pi-cog',
      command: () => this.closeMenuAndNavigate('/platform/settings')
    },
    {
      separator: true
    },
    {
      label: '个人信息',
      icon: 'pi pi-user-edit',
      command: () => this.openProfileDialog()
    },
    {
      label: '修改密码',
      icon: 'pi pi-lock',
      command: () => this.openChangePasswordDialog()
    },
    {
      separator: true
    },
    {
      label: '退出登录',
      icon: 'pi pi-sign-out',
      command: () => this.handleLogout()
    }
  ];

  openProfileDialog(): void {
    this.forceCloseMenu();
    this.profileDialogVisible.set(true);
  }

  openChangePasswordDialog(): void {
    this.forceCloseMenu();
    this.profileDialogVisible.set(false);
    this.changePasswordDialogVisible.set(true);
  }

  handleLogout(): void {
    this.authService.logout();
    this.closeMenuAndNavigate('/auth/login');
  }

  private closeMenuAndNavigate(path: string): void {
    this.forceCloseMenu();
    this.router.navigate([path]);
  }

  private forceCloseMenu(): void {
    try {
      if (this.userMenu) {
        this.userMenu.hide();
      }
    } catch {
      // 忽略关闭异常，由兜底清理机制处理
    } finally {
      setTimeout(() => this.cleanupOverlay(), 50);
    }
  }

  private cleanupOverlay(): void {
    const overlays = document.querySelectorAll('.p-menu-overlay, .p-component-overlay');
    if (overlays.length > 0) {
      overlays.forEach(overlay => overlay.remove());
    }
  }

  ngOnDestroy(): void {
    this.forceCloseMenu();
  }
}
