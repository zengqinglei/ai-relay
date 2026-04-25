import { UsageSource } from '../models/usage-source.enum';

export const USAGE_SOURCE_LABELS: Record<UsageSource, string> = {
  [UsageSource.ApiProxy]: 'API 代理',
  [UsageSource.WorkspaceChat]: '工作区聊天',
  [UsageSource.ModelTest]: '模型测试'
};

export const USAGE_SOURCE_OPTIONS = Object.entries(USAGE_SOURCE_LABELS).map(([value, label]) => ({
  label,
  value: value as UsageSource
}));
