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
import { ThemeConfigurator } from '../../../shared/components/theme-configurator/theme-configurator';
import { LayoutService } from '../../services/layout-service';

@Component({
  selector: 'app-default-header',
  standalone: true,
  imports: [RouterModule, ButtonModule, AvatarModule, BadgeModule, StyleClassModule, TooltipModule, MenuModule, ThemeConfigurator],
  templateUrl: './default-header.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DefaultHeader implements OnDestroy {
  readonly layoutService = inject(LayoutService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  @ViewChild('userMenu') userMenu!: Menu;

  readonly toggleMobileMenu = output<void>();
  notificationCount = signal(1);

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
      label: '退出登录',
      icon: 'pi pi-sign-out',
      command: () => this.closeMenuAndNavigate('/auth/login')
    }
  ];

  /**
   * 关闭菜单并导航
   */
  private closeMenuAndNavigate(path: string): void {
    this.forceCloseMenu();
    this.router.navigate([path]);
  }

  /**
   * 强制关闭菜单（包含容错处理）
   */
  private forceCloseMenu(): void {
    try {
      if (this.userMenu) {
        this.userMenu.hide();
      }
    } catch (_error) {
      // 忽略关闭异常，由兜底清理机制处理
    } finally {
      // 无论是否成功关闭，都手动清理 DOM 中的 overlay
      setTimeout(() => this.cleanupOverlay(), 50);
    }
  }

  /**
   * 清理残留的 overlay 元素
   */
  private cleanupOverlay(): void {
    const overlays = document.querySelectorAll('.p-menu-overlay, .p-component-overlay');
    if (overlays.length > 0) {
      overlays.forEach(overlay => overlay.remove());
    }
  }

  /**
   * 组件销毁时强制关闭菜单
   */
  ngOnDestroy(): void {
    this.forceCloseMenu();
  }
}
