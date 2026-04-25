import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { DrawerModule } from 'primeng/drawer';

import { DefaultFooter } from '../components/default-footer/default-footer';
import { DefaultHeader } from '../components/default-header/default-header';
import { DefaultSidebar } from '../components/default-sidebar/default-sidebar';
import { LayoutService } from '../services/layout-service';

@Component({
  selector: 'app-default-layout',
  standalone: true,
  imports: [RouterOutlet, DrawerModule, DefaultHeader, DefaultFooter, DefaultSidebar],
  templateUrl: './default-layout.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DefaultLayout {
  layoutService = inject(LayoutService);

  isMobileMenuOpen = signal(false);

  constructor() {
    effect(() => {
      if (!this.layoutService.isMobileSidebarMode()) {
        this.closeMobileMenu();
      }
    });
  }

  toggleMobileMenu() {
    this.isMobileMenuOpen.update(v => !v);
  }

  closeMobileMenu() {
    this.isMobileMenuOpen.set(false);
  }
}
