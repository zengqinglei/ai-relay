export enum Role {
  Admin = 'Admin',
  Operator = 'Operator',
  Member = 'Member'
}

export const ROLE_LABEL_MAP: Record<Role, string> = {
  [Role.Admin]: '管理员',
  [Role.Operator]: '运营人员',
  [Role.Member]: '普通成员'
};
