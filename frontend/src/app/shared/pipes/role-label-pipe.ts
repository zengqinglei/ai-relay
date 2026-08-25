import { Pipe, PipeTransform } from '@angular/core';

import { ROLE_LABEL_MAP } from '../models/role.enum';

/**
 * 角色名称中文映射 Pipe
 *
 * 用法：{{ role | roleLabel }}
 */
@Pipe({
  name: 'roleLabel',
  standalone: true
})
export class RoleLabelPipe implements PipeTransform {
  transform(value: string | undefined | null): string {
    if (!value) return '-';
    return ROLE_LABEL_MAP[value as keyof typeof ROLE_LABEL_MAP] ?? value;
  }
}

/**
 * 获取角色中文名称（非 Pipe 场景使用）
 */
export function getRoleLabel(role: string): string {
  return ROLE_LABEL_MAP[role as keyof typeof ROLE_LABEL_MAP] ?? role;
}
