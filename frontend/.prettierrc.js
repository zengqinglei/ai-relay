module.exports = {
  singleQuote: true,
  printWidth: 140,
  htmlWhitespaceSensitivity: 'strict',
  arrowParens: 'avoid',
  trailingComma: 'none',
  endOfLine: 'auto',
  overrides: [
    {
      files: '*.html',
      options: {
        parser: 'angular'
      }
    }
  ]
};
