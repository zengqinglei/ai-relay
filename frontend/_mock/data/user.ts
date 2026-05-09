import { UserOutputDto } from '../../src/app/features/account/models/account.dto';
import { UserManagementOutputDto } from '../../src/app/features/platform/models/user-management.dto';

export interface MockUser extends UserOutputDto {
  password: string;
  isEmailVerified: boolean;
  lastLoginTime?: string;
}

export const USERS: MockUser[] = [
  {
    id: '00000000-0000-0000-0000-000000000001',
    username: 'admin',
    email: 'admin@example.com',
    nickname: '管理员',
    avatar: 'https://api.dicebear.com/7.x/avataaars/svg?seed=admin',
    phoneNumber: '13800000000',
    isActive: true,
    isSuperAdmin: true,
    isEmailVerified: true,
    creationTime: '2024-01-01T00:00:00Z',
    lastLoginTime: '2026-05-08T09:30:00Z',
    roles: ['Admin', 'Member'],
    password: 'Admin@123456'
  },
  {
    id: '00000000-0000-0000-0000-000000000002',
    username: 'zengql',
    email: 'zengql@example.com',
    nickname: '曾启龙',
    avatar: 'https://api.dicebear.com/7.x/avataaars/svg?seed=zengql',
    phoneNumber: '13900000000',
    isActive: true,
    isSuperAdmin: false,
    isEmailVerified: true,
    creationTime: '2024-06-01T00:00:00Z',
    lastLoginTime: '2026-05-07T15:12:00Z',
    roles: ['Member'],
    password: 'Zengql@123456'
  },
  {
    id: '00000000-0000-0000-0000-000000000003',
    username: 'operator',
    email: 'operator@example.com',
    nickname: '运营管理员',
    avatar: 'https://api.dicebear.com/7.x/avataaars/svg?seed=operator',
    phoneNumber: '13700000000',
    isActive: true,
    isSuperAdmin: false,
    isEmailVerified: false,
    creationTime: '2025-03-12T08:00:00Z',
    roles: ['Operator'],
    password: 'Operator@123456'
  },
  {
    id: '00000000-0000-0000-0000-000000000004',
    username: 'disabled-user',
    email: 'disabled@example.com',
    nickname: '已禁用用户',
    avatar: 'https://api.dicebear.com/7.x/avataaars/svg?seed=disabled',
    phoneNumber: '13600000000',
    isActive: false,
    isSuperAdmin: false,
    isEmailVerified: true,
    creationTime: '2025-09-20T10:20:00Z',
    lastLoginTime: '2026-01-03T11:45:00Z',
    roles: ['Member'],
    password: 'Disabled@123456'
  }
];

export function toUserOutput(user: MockUser): UserOutputDto {
  const { password: _password, isEmailVerified: _isEmailVerified, lastLoginTime: _lastLoginTime, ...output } = user;
  return output;
}

export function toUserManagementOutput(user: MockUser): UserManagementOutputDto {
  return {
    id: user.id,
    username: user.username,
    email: user.email,
    displayName: user.nickname,
    avatar: user.avatar,
    isActive: user.isActive,
    isEmailVerified: user.isEmailVerified,
    roles: user.roles,
    isSuperAdmin: user.isSuperAdmin,
    creationTime: user.creationTime,
    lastLoginTime: user.lastLoginTime
  };
}
