import { Routes } from '@angular/router';

/**
 * 平台管理模块路由配置
 * 用于 Default Layout 的子路由
 * 需要超级管理员权限
 */
export const PLATFORM_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./components/dashboard/dashboard').then(m => m.Dashboard)
  },
  {
    path: 'account-tokens',
    loadComponent: () => import('./components/account-token/account-token').then(m => m.AccountTokenPage)
  },
  {
    path: 'provider-groups',
    loadComponent: () => import('./components/provider-group/provider-group').then(m => m.ProviderGroupPage)
  },
  {
    path: 'subscriptions',
    loadComponent: () => import('./components/subscriptions/subscriptions').then(m => m.SubscriptionsPage)
  },
  {
    path: 'usage-records',
    loadComponent: () => import('./components/usage-records/usage-records').then(m => m.UsageRecords)
  },
  {
    path: 'settings',
    loadComponent: () => import('./components/settings/settings').then(m => m.Settings)
  },
  {
    path: 'infrastructure',
    loadComponent: () => import('./components/infrastructure/infrastructure-overview').then(m => m.InfrastructureOverview)
  }
];
