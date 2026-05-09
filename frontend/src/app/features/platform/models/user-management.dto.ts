import { PagedRequestDto } from '../../../shared/models/paged-request.dto';

export interface UserManagementOutputDto {
  id: string;
  username: string;
  email: string;
  displayName?: string;
  avatar?: string;
  isActive: boolean;
  isEmailVerified: boolean;
  roles: string[];
  isSuperAdmin: boolean;
  creationTime: string;
  lastLoginTime?: string;
}

export interface GetUsersInputDto extends PagedRequestDto {
  keyword?: string;
  isActive?: boolean;
  isEmailVerified?: boolean;
  role?: string;
}

export interface CreateUserInputDto {
  username: string;
  email: string;
  displayName?: string;
  avatar?: string;
  password: string;
  isActive: boolean;
  isEmailVerified: boolean;
  roles: string[];
}

export interface UpdateUserInputDto {
  email: string;
  displayName?: string;
  avatar?: string;
  isActive: boolean;
  isEmailVerified: boolean;
  roles: string[];
}

export interface ResetUserPasswordInputDto {
  password: string;
}
