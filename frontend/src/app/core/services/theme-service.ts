import { isPlatformBrowser } from '@angular/common';
import { Injectable, signal, effect, inject, PLATFORM_ID } from '@angular/core';

/**
 * 主题服务
 * 负责管理应用的深色/浅色主题切换
 *
 * 采用 CSS Transitions 实现平滑的主题切换动画 (Performance optimized)
 * 遵循 Angular 21 最佳实践和 PrimeNG 21 主题系统
 *
 * @see https://primeng.org/theming/styled
 */
@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private readonly THEME_KEY = 'app_theme';
  private readonly platformId = inject(PLATFORM_ID);

  /**
   * 深色主题状态 Signal
   * 使用 signal 实现细粒度响应式更新
   */
  darkTheme = signal<boolean>(this.loadThemePreference());

  constructor() {
    // 使用 effect 监听主题变化并应用到 DOM
    // Angular 21 推荐在 effect 中处理副作用
    effect(() => {
      this.applyTheme(this.darkTheme());
    });
  }

  /**
   * 切换主题模式
   * 采用 View Transition API 实现标准、平滑的交叉淡入淡出效果
   */
  toggleTheme(): void {
    const isDark = !this.darkTheme();

    // 1. 特性检测：如果浏览器不支持 View Transition API (如旧版 Firefox)
    if (!document.startViewTransition) {
      this.darkTheme.set(isDark);
      return;
    }

    // 2. 使用浏览器原生 View Transition 实现平滑切换
    // 浏览器会自动对切换前后的页面进行快照并执行交叉淡入淡出动画
    document.startViewTransition(() => {
      this.darkTheme.set(isDark);
    });
  }

  /**
   * 应用主题到 DOM
   * 直接切换 .dark 类，依赖 CSS transitions 实现平滑过渡
   *
   * @param isDark - 是否启用深色主题
   */
  private applyTheme(isDark: boolean): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.updateDarkClass(isDark);
    this.saveThemePreference(isDark);
  }

  /**
   * 更新 document.documentElement 的 .dark 类
   *
   * 这是主题切换的核心操作：
   * - 添加 .dark 类触发 Tailwind 的 dark: 变体
   * - PrimeNG 通过 darkModeSelector 监听此类
   * - 所有组件自动切换到暗色主题变量
   *
   * @param isDark - 是否启用深色主题
   */
  private updateDarkClass(isDark: boolean): void {
    const htmlElement = document.documentElement;

    if (isDark) {
      htmlElement.classList.add('dark');
    } else {
      htmlElement.classList.remove('dark');
    }
  }

  /**
   * 加载主题偏好
   *
   * 优先级：
   * 1. localStorage 中的用户偏好
   * 2. 系统主题偏好 (prefers-color-scheme)
   * 3. 默认浅色主题
   *
   * @returns 是否启用深色主题
   */
  private loadThemePreference(): boolean {
    if (!isPlatformBrowser(this.platformId)) {
      return false;
    }

    const saved = localStorage.getItem(this.THEME_KEY);
    if (saved !== null) {
      return saved === 'dark';
    }

    // 默认跟随系统主题偏好
    // 遵循用户操作系统设置
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
  }

  /**
   * 保存主题偏好到 localStorage
   * 实现主题持久化
   *
   * @param isDark - 是否启用深色主题
   */
  private saveThemePreference(isDark: boolean): void {
    if (isPlatformBrowser(this.platformId)) {
      localStorage.setItem(this.THEME_KEY, isDark ? 'dark' : 'light');
    }
  }
}
