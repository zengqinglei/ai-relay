/**
 * 统一的 Tooltip 样式常量（PrimeNG pTooltip）
 *
 * 使用规范：
 * - SM  : 简短提示文字（按钮操作说明、单行说明），约 200px
 * - MD  : 中等内容（名称、分组、账户等截断文字），约 320px（Tailwind max-w-xs）
 * - LG  : 长内容（URL、UserAgent、状态描述、多行说明），约 448px（Tailwind max-w-md）
 */
export const TOOLTIP_STYLE = {
  /** 简短提示，无需限宽（PrimeNG 默认宽度） */
  SM: undefined as string | undefined,

  /** 中等内容，对应 Tailwind max-w-xs (~320px) */
  MD: 'max-w-xs',

  /** 长内容，对应 Tailwind max-w-md (~448px) */
  LG: 'max-w-md',
} as const;
