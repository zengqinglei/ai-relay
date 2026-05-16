import { Routes } from '@angular/router';

export const WORKSPACE_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'dashboard'
  },
  // Workspace chat is temporarily disabled for external users.
  // {
  //   path: 'chat',
  //   loadComponent: () => import('./components/chat/workspace-chat').then(m => m.WorkspaceChatPage)
  // },
  // {
  //   path: 'chat/:sessionId',
  //   loadComponent: () => import('./components/chat/workspace-chat').then(m => m.WorkspaceChatPage)
  // },
  {
    path: 'dashboard',
    loadComponent: () => import('./components/dashboard/workspace-dashboard').then(m => m.WorkspaceDashboardPage)
  },
  {
    path: 'my-subscriptions',
    loadComponent: () => import('./components/my-subscriptions/workspace-my-subscriptions').then(m => m.WorkspaceMySubscriptionsPage)
  },
  {
    path: 'usage-logs',
    loadComponent: () => import('./components/usage-logs/workspace-usage-logs').then(m => m.WorkspaceUsageLogsPage)
  }
];
