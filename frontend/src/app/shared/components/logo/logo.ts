import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-logo',
  templateUrl: './logo.html',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LogoComponent {
  /** 控制 Logo 的尺寸: 'normal' 用于导航栏或按钮, 'large' 用于登录页或展示面板 */
  size = input<'normal' | 'large'>('normal');
}
