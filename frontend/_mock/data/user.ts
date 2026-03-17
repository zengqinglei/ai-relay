import { UserOutputDto } from '../../src/app/features/account/models/account.dto';

export const USERS: UserOutputDto[] = [
  {
    id: '00000000-0000-0000-0000-000000000001',
    username: 'admin',
    email: 'admin@example.com',
    nickname: '管理员',
    avatarUrl: 'https://api.dicebear.com/7.x/avataaars/svg?seed=admin',
    isActive: true,
    creationTime: '2024-01-01T00:00:00Z',
    roles: ['Admin', 'Member']
  }
];
