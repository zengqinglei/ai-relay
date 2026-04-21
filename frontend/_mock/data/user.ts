import { UserOutputDto } from '../../src/app/features/account/models/account.dto';

export interface MockUser extends UserOutputDto {
  password: string;
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
    creationTime: '2024-01-01T00:00:00Z',
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
    creationTime: '2024-06-01T00:00:00Z',
    roles: ['Member'],
    password: 'Zengql@123456'
  }
];

export function toUserOutput(user: MockUser): UserOutputDto {
  const { password: _password, ...output } = user;
  return output;
}
