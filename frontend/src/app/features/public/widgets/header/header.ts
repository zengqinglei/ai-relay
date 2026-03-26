import { Component, inject, signal, HostListener } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { RippleModule } from 'primeng/ripple';
import { StyleClassModule } from 'primeng/styleclass';
import { TooltipModule } from 'primeng/tooltip';
import { LayoutService } from '../../../../layout/services/layout-service';

import { LogoComponent } from '../../../../shared/components/logo/logo';
import { ThemeConfigurator } from '../../../../shared/components/theme-configurator/theme-configurator';

@Component({
  selector: 'app-landing-header',
  standalone: true,
  imports: [RouterModule, StyleClassModule, ButtonModule, RippleModule, TooltipModule, LogoComponent, ThemeConfigurator],
  templateUrl: './header.html'
})
export class LandingHeader {
  private router = inject(Router);
  public layoutService = inject(LayoutService);

  // 移动端菜单状态
  protected isMobileMenuOpen = signal(false);

  /**
   * 切换移动端菜单显示状态
   */
  toggleMobileMenu(): void {
    this.isMobileMenuOpen.update(isOpen => !isOpen);
  }

  /**
   * 监听窗口大小变化，大屏幕时自动关闭移动端菜单
   */
  @HostListener('window:resize')
  onResize(): void {
    if (window.innerWidth >= 1024) {
      this.isMobileMenuOpen.set(false);
    }
  }

  /**
   * 导航到页面指定区域
   */
  navigateToSection(fragment: string): void {
    this.router.navigate(['/landing'], { fragment });
    // 导航后关闭移动端菜单
    this.isMobileMenuOpen.set(false);
  }
}
