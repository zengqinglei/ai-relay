/**
 * 格式化工具函数
 * 提供数字、Token等数据的格式化方法
 */

/**
 * 格式化 Token 数量（K, M, B）
 *
 * @param num Token数量
 * @returns 格式化后的字符串
 * @example
 * formatTokenCount(1234) => "1.23K"
 * formatTokenCount(1234567) => "1.23M"
 * formatTokenCount(1234567890) => "1.23B"
 */
export function formatTokenCount(num: number): string {
  if (num >= 1_000_000_000) return `${(num / 1_000_000_000).toFixed(2)}B`;
  if (num >= 1_000_000) return `${(num / 1_000_000).toFixed(2)}M`;
  if (num >= 1_000) return `${(num / 1_000).toFixed(2)}K`;
  return num.toLocaleString('zh-CN');
}

/**
 * 格式化数字（添加千位分隔符）
 *
 * @param num 数字
 * @returns 格式化后的字符串
 * @example
 * formatNumber(1234567) => "1,234,567"
 */
export function formatNumber(num: number): string {
  return num.toLocaleString('zh-CN');
}
