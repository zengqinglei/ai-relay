import { bootstrapApplication } from '@angular/platform-browser';

import { App } from './app/app';
import { appConfig } from './app/app.config';

bootstrapApplication(App, appConfig)
  // 关键修改: 在 Promise 的 then 回调中执行 DOM 操作
  .then(() => {
    const preloader = document.getElementById('preloader');
    if (preloader) {
      // 先添加一个淡出效果的 class
      preloader.classList.add('opacity-0');
      // 在动画结束后，从 DOM 中彻底移除该元素
      setTimeout(() => preloader.remove(), 300); // 300ms 匹配 transition-duration
    }
  })
  .catch(err => console.error(err));
