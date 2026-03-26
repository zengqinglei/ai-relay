import { isPlatformBrowser } from '@angular/common';
import { Injectable, signal, inject, PLATFORM_ID, DestroyRef, computed, effect } from '@angular/core';

export interface LayoutConfig {
  preset?: string;
  primary?: string;
  surface?: string | undefined | null;
  darkTheme?: boolean;
  menuMode?: string;
}

/**
 * 布局服务
 * 管理应用布局的状态，包括侧边栏的可见性和折叠状态
 * 支持响应式布局，在小屏幕设备上自动折叠侧边栏
 */
@Injectable({
  providedIn: 'root'
})
export class LayoutService {
  /**
   * 控制侧边栏的可见性
   * 主要用于移动端的显示/隐藏
   */
  sidebarVisible = signal<boolean>(true);

  /**
   * 控制侧边栏的折叠状态
   * true: 折叠（仅显示图标）
   * false: 展开（显示完整内容）
   */
  sidebarCollapsed = signal<boolean>(true);

  /**
   * 页面标题
   */
  title = signal<string>('概览');

  private readonly platformId = inject(PLATFORM_ID);
  private readonly destroyRef = inject(DestroyRef);
  private mediaQueryList?: MediaQueryList;
  private mediaQueryHandler?: (e: MediaQueryListEvent) => void;
  private initialized = false;
  private readonly LAYOUT_CONFIG_KEY = 'layout_config';

  /**
   * 主题配置
   */
  layoutConfig = signal<LayoutConfig>(this.getInitialConfig());

  isDarkTheme = computed(() => this.layoutConfig().darkTheme);

  constructor() {
    this.initResponsive();

    // 立即应用初始主题配置到DOM（仅在浏览器环境）
    if (isPlatformBrowser(this.platformId)) {
      const initialConfig = this.layoutConfig();
      if (initialConfig.darkTheme) {
        document.documentElement.classList.add('dark');
      } else {
        document.documentElement.classList.remove('dark');
      }
    }

    // 注册清理函数，在服务销毁时移除监听器
    this.destroyRef.onDestroy(() => {
      this.cleanup();
    });

    effect(() => {
      const config = this.layoutConfig();
      if (isPlatformBrowser(this.platformId)) {
        localStorage.setItem(this.LAYOUT_CONFIG_KEY, JSON.stringify(config));
      }

      if (!this.initialized || !config) {
        this.initialized = true;
        return;
      }
      this.handleDarkModeTransition(config);
    });
  }

  private getInitialConfig(): LayoutConfig {
    const defaultConfig: LayoutConfig = {
      preset: 'Aura',
      primary: 'blue',
      surface: 'slate',
      darkTheme: true,
      menuMode: 'static'
    };

    if (isPlatformBrowser(inject(PLATFORM_ID))) {
      const storedConfig = localStorage.getItem('layout_config'); // Access key directly or move const up if static
      if (storedConfig) {
        try {
          return { ...defaultConfig, ...JSON.parse(storedConfig) };
        } catch (e) {
          console.error('Failed to parse layout config', e);
        }
      }
    }
    return defaultConfig;
  }

  private handleDarkModeTransition(config: LayoutConfig): void {
    if (isPlatformBrowser(this.platformId)) {
      if ((document as any).startViewTransition) {
        this.startViewTransition(config);
      } else {
        this.toggleDarkMode(config);
      }
    }
  }

  private startViewTransition(config: LayoutConfig): void {
    const _transition = (document as any).startViewTransition(() => {
      this.toggleDarkMode(config);
    });
  }

  toggleDarkMode(config?: LayoutConfig): void {
    const _config = config || this.layoutConfig();
    // Use update to trigger signals and effects
    if (!config) {
      this.layoutConfig.update(state => ({ ...state, darkTheme: !state.darkTheme }));
      // The effect in constructor will call this method again with the updated config,
      // but we need to ensure the class is toggled immediately for responsiveness if called directly
      // However, better to let the effect handle the class toggling to avoid double toggling or race conditions
      // But wait, the effect calls handleDarkModeTransition which calls toggleDarkMode.
      // We need to distinguish between 'user action' and 'effect application'.
      return;
    }

    if (_config.darkTheme) {
      document.documentElement.classList.add('dark');
    } else {
      document.documentElement.classList.remove('dark');
    }
  }

  /**
   * 初始化响应式布局
   * 在浏览器环境中监听屏幕宽度变化，自动调整侧边栏折叠状态
   * 断点: 1024px (Tailwind 的 lg 断点)
   */
  private initResponsive(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.mediaQueryList = window.matchMedia('(max-width: 1024px)');

      // 根据当前屏幕宽度初始化状态
      if (this.mediaQueryList.matches) {
        this.sidebarCollapsed.set(true);
      }

      // 保存处理函数引用以便后续移除
      this.mediaQueryHandler = (e: MediaQueryListEvent) => {
        this.sidebarCollapsed.set(e.matches);
      };

      // 监听屏幕宽度变化
      this.mediaQueryList.addEventListener('change', this.mediaQueryHandler);
    }
  }

  /**
   * 清理资源
   * 移除 MediaQuery 监听器，防止内存泄漏
   */
  private cleanup(): void {
    if (this.mediaQueryList && this.mediaQueryHandler) {
      this.mediaQueryList.removeEventListener('change', this.mediaQueryHandler);
    }
  }

  /**
   * 切换侧边栏折叠状态
   * 用于用户手动展开/折叠侧边栏
   */
  toggleSidebarCollapse(): void {
    this.sidebarCollapsed.update(collapsed => !collapsed);
  }

  /**
   * 切换侧边栏可见性
   * 主要用于移动端的侧边栏显示/隐藏
   */
  toggleSidebar(): void {
    this.sidebarVisible.update(visible => !visible);
  }
}
