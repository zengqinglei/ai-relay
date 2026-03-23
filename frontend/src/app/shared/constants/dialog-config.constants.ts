/**
 * 统一的 Dialog 配置常量
 * 基于 PrimeNG 最佳实践，提供三种标准尺寸配置
 */

export interface DialogConfig {
  breakpoints: { [key: string]: string };
  style: { [key: string]: string };
  contentStyle: { [key: string]: string };
  draggable: boolean;
  resizable: boolean;
}

/**
 * Dialog 配置预设
 */
export const DIALOG_CONFIGS = {
  /**
   * 小型对话框 - 适用于简单表单
   * 例如：渠道账户编辑、订阅编辑
   */
  SMALL: {
    breakpoints: { '1199px': '75vw', '575px': '90vw' },
    style: { width: '50vw', 'max-width': '700px' },
    contentStyle: { 'max-height': 'calc(100vh - 180px)', 'overflow-y': 'auto' },
    draggable: false,
    resizable: false
  } as DialogConfig,

  /**
   * 中型对话框 - 适用于复杂表单或一般详情
   * 例如：渠道分组编辑、模型测试、账户详情
   */
  MEDIUM: {
    breakpoints: { '1199px': '80vw', '575px': '95vw' },
    style: { width: '60vw', 'max-width': '900px' },
    contentStyle: { 'max-height': 'calc(100vh - 180px)', 'overflow-y': 'auto' },
    draggable: false,
    resizable: false
  } as DialogConfig,

  /**
   * 大型对话框 - 适用于详细信息展示
   * 例如：使用记录详情
   */
  LARGE: {
    breakpoints: { '1199px': '85vw', '575px': '95vw' },
    style: { width: '70vw', 'max-width': '1200px' },
    contentStyle: { 'max-height': 'calc(100vh - 180px)', 'overflow-y': 'auto' },
    draggable: false,
    resizable: false
  } as DialogConfig
};
