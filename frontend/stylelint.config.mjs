export default {
  // 直接继承 standard 和 clean-order，后者会自动处理属性排序
  extends: ['stylelint-config-standard', 'stylelint-config-clean-order'],
  plugins: ['stylelint-declaration-block-no-ignored-properties'],
  rules: {
    // 启用插件的规则
    'plugin/declaration-block-no-ignored-properties': true,
    // 兼容 Angular
    'selector-type-no-unknown': [
      true,
      {
        ignoreTypes: ['app-']
      }
    ],
    'selector-pseudo-element-no-unknown': [
      true,
      {
        ignorePseudoElements: ['ng-deep']
      }
    ],
    // 兼容 Tailwind CSS v4
    'at-rule-no-unknown': [
      true,
      {
        ignoreAtRules: ['tailwind', 'plugin', 'custom-variant']
      }
    ],
    // 根据 ng-alain 的实践，关闭一些可能过于严格或与现代CSS实践冲突的规则
    'function-no-unknown': null,
    'no-descending-specificity': null,
    'import-notation': 'string',
    'media-feature-range-notation': 'prefix'
  },
  ignoreFiles: ['src/assets/**/*', 'node_modules/**/*', 'dist/**/*']
};
