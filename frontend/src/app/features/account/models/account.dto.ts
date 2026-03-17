/**
 * 登录请求 DTO
 */
export interface LoginInputDto {
  usernameOrEmail: string;
  password: string;
}

/**
 * 登录响应 DTO（不包含用户信息）
 */
export interface LoginOutputDto {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

/**
 * 用户输出 DTO
 */
export interface UserOutputDto {
  id: string;
  username: string;
  email: string;
  nickname?: string;
  avatarUrl?: string;
  phoneNumber?: string;
  isActive: boolean;
  creationTime: string;
  roles: string[];
}

/**
 * 第三方登录 URL 响应 DTO
 */
export interface ExternalLoginUrlOutputDto {
  loginUrl: string;
  state: string;
}

/**
 * 第三方登录回调请求 DTO
 */
export interface ExternalLoginCallbackInputDto {
  provider: string;
  code: string;
  state: string;
}

/**
 * 用户注册请求 DTO
 */
export interface RegisterInputDto {
  username: string;
  email: string;
  password: string;
  nickname?: string;
}
