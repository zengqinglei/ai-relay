import { Routes } from '@angular/router';

/**
 * 工作区模块路由配置
 * 用于 Default Layout 的子路由
 */
export const WORKSPACE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./components/placeholder/workspace-placeholder').then(m => m.WorkspacePlaceholder)
  }
];
