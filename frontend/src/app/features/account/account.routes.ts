import { Routes } from '@angular/router';

/**
 * 认证模块路由配置
 * 用于 Empty Layout 的子路由
 */
export const AUTH_ROUTES: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./components/login/login').then(m => m.Login)
  },
  {
    path: 'register',
    loadComponent: () => import('./components/register/register').then(m => m.Register)
  },
  {
    path: 'external-callback',
    loadComponent: () =>
      import('./components/external-auth-callback/external-auth-callback').then(m => m.ExternalAuthCallback)
  },

  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full'
  }
];

export default AUTH_ROUTES;
