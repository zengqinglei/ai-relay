import { Routes } from '@angular/router';

/**
 * 公共页面路由配置
 * 用于 Landing Layout 的子路由
 */
export const LANDING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./components/landing/landing').then(m => m.Landing)
  }
];

/**
 * 公共页面路由配置（通用）
 * 用于其他需要公共页面的场景
 */
export const PUBLIC_ROUTES: Routes = [
  // 暂时为空，后续添加其他公共页面
];
