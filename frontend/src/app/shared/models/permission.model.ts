export interface Permission {
  roles: string[];
  permissions: PermissionItem[];
}

export interface PermissionItem {
  code: string;
  description: string;
}
