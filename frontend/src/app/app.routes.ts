import { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth-guard';
import { roleGuard } from './core/guards/role-guard';
// 布局组件导入
import { DefaultLayout } from './layout/default/default-layout';
import { EmptyLayout } from './layout/empty/empty-layout';

export const routes: Routes = [
  // 默认重定向到推广页
  {
    path: '',
    redirectTo: '/landing',
    pathMatch: 'full'
  },

  // Landing Layout - 产品推广页面
  {
    path: 'landing',
    loadChildren: () => import('./features/public/public.routes').then(r => r.LANDING_ROUTES)
  },

  // Empty Layout - 认证相关页面（登录、注册等）
  {
    path: 'auth',
    component: EmptyLayout,
    loadChildren: () => import('./features/account/account.routes').then(r => r.AUTH_ROUTES)
  },

  // Default Layout - 用户工作区
  {
    path: 'workspace',
    component: DefaultLayout,
    canActivate: [authGuard],
    loadChildren: () => import('./features/workspace/workspace.routes').then(r => r.WORKSPACE_ROUTES)
  },

  // Default Layout - 平台管理
  {
    path: 'platform',
    component: DefaultLayout,
    canActivate: [authGuard, roleGuard],
    data: { role: 'Admin' },
    loadChildren: () => import('./features/platform/platform.routes').then(r => r.PLATFORM_ROUTES)
  },

  // 兜底路由
  { path: '**', redirectTo: '' }
];
