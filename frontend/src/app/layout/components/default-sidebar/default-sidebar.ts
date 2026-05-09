import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { filter, map, startWith } from 'rxjs/operators';

import { LayoutService } from '../../services/layout-service';

interface MenuItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-default-sidebar',
  standalone: true,
  imports: [NgClass, RouterModule, ButtonModule, TooltipModule],
  templateUrl: './default-sidebar.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DefaultSidebar {
  readonly layoutService = inject(LayoutService);
  private readonly router = inject(Router);

  isMobileMenuOpen = input<boolean>(false);
  readonly mobileMenuClosed = output<void>();

  private readonly platformMenuItems: MenuItem[] = [
    { label: '仪表盘', icon: 'pi-gauge', route: '/platform' },
    { label: '渠道账户', icon: 'pi-credit-card', route: '/platform/account-tokens' },
    { label: '订阅管理', icon: 'pi-key', route: '/platform/subscriptions' },
    { label: '分组管理', icon: 'pi-sitemap', route: '/platform/provider-groups' },
    { label: '使用记录', icon: 'pi-history', route: '/platform/usage-records' },
    { label: '开放应用', icon: 'pi-id-card', route: '/platform/open-applications' },
    { label: '用户管理', icon: 'pi-users', route: '/platform/users' },
    { label: '系统设置', icon: 'pi-cog', route: '/platform/settings' }
  ];
  private readonly workspaceMenuItems: MenuItem[] = [
    { label: '聊天', icon: 'pi-comments', route: '/workspace/chat' },
    { label: '仪表盘', icon: 'pi-gauge', route: '/workspace/dashboard' },
    { label: '我的订阅', icon: 'pi-key', route: '/workspace/my-subscriptions' },
    { label: '使用日志', icon: 'pi-history', route: '/workspace/usage-logs' }
  ];

  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map(event => event.urlAfterRedirects ?? event.url),
      startWith(this.router.url)
    ),
    { initialValue: this.router.url }
  );

  readonly menuItems = computed(() => {
    const url = this.currentUrl();
    return url.startsWith('/platform') ? this.platformMenuItems : this.workspaceMenuItems;
  });

  isItemActive(item: MenuItem) {
    const currentUrl = this.currentUrl();
    if (item.route === '/platform') {
      return currentUrl === item.route;
    }

    return currentUrl === item.route || currentUrl.startsWith(`${item.route}/`);
  }
}
