export class User {
  id!: string;
  username!: string;
  email!: string;
  nickname?: string;
  avatarUrl?: string;
  roles!: string[];

  constructor(data: Partial<User>) {
    Object.assign(this, data);
  }

  /**
   * 检查是否为管理员
   * 后端仅一种管理员角色：'Admin'
   */
  isAdmin(): boolean {
    return this.roles.includes('Admin');
  }
}
