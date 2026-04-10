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
    { label: '资源池', icon: 'pi-sitemap', route: '/platform/provider-groups' },
    { label: '订阅管理', icon: 'pi-key', route: '/platform/subscriptions' },
    { label: '使用记录', icon: 'pi-chart-line', route: '/platform/usage-records' },
    { label: '系统设置', icon: 'pi-cog', route: '/platform/settings' }
  ];
  private readonly workspaceMenuItems: MenuItem[] = [{ label: '工作区', icon: 'pi-briefcase', route: '/workspace' }];

  // Create a signal for the current URL
  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map(event => event.urlAfterRedirects ?? event.url),
      startWith(this.router.url)
    ),
    { initialValue: this.router.url }
  );

  // Derive menu items based on the URL signal
  readonly menuItems = computed(() => {
    const url = this.currentUrl();
    return url.startsWith('/platform') ? this.platformMenuItems : this.workspaceMenuItems;
  });
}
