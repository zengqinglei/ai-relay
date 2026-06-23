import { HttpContextToken } from '@angular/common/http';

/** 静默认证请求：不显示错误提示，不处理 401 跳转 */
export const SILENT_AUTH = new HttpContextToken<boolean>(() => false);
