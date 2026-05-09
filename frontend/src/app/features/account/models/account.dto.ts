/**
 * 登录请求 DTO
 */
export interface LoginInputDto {
  usernameOrEmail: string;
  password: string;
}

/**
 * 用户输出 DTO
 */
export interface UserOutputDto {
  id: string;
  username: string;
  email: string;
  nickname?: string;
  avatar?: string;
  phoneNumber?: string;
  isActive: boolean;
  isSuperAdmin: boolean;
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
  captchaCode?: string;
  captchaToken?: string;
  emailVerificationCode?: string;
  nickname?: string;
}

/**
 * 注册安全配置输出 DTO
 */
export interface SecurityConfigOutputDto {
  enableEmailVerification: boolean;
}

/**
 * 图形验证码输出 DTO
 */
export interface CaptchaOutputDto {
  captchaToken: string;
  captchaImageBase64: string;
}

/**
 * 发送邮件验证码请求 DTO
 */
export interface SendEmailCodeInputDto {
  email: string;
  captchaToken: string;
  captchaCode: string;
}

/**
 * 当前用户资料更新请求 DTO
 */
export interface UpdateCurrentUserInputDto {
  username: string;
  email: string;
  nickname?: string;
  phoneNumber?: string;
  avatar?: string;
}

/**
 * 修改密码请求 DTO
 */
export interface ChangePasswordInputDto {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}
