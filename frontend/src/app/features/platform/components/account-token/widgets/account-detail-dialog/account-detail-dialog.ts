import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';

import { PlatformIcon } from '../../../../../../shared/components/platform-icon/platform-icon';
import { DIALOG_CONFIGS } from '../../../../../../shared/constants/dialog-config.constants';
import { PROVIDER_PLATFORM_LABELS } from '../../../../../../shared/constants/provider-platform.constants';
import { ProviderPlatform } from '../../../../../../shared/models/provider-platform.enum';
import { AccountTokenOutputDto } from '../../../../models/account-token.dto';

@Component({
  selector: 'app-account-detail-dialog',
  standalone: true,
  imports: [CommonModule, DialogModule, ButtonModule, TooltipModule, PlatformIcon],
  templateUrl: './account-detail-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AccountDetailDialogComponent {
  @Input() visible = false;
  @Input() account: AccountTokenOutputDto | null = null;
  @Output() readonly visibleChange = new EventEmitter<boolean>();

  ProviderPlatform = ProviderPlatform;

  // 使用中型 Dialog 配置
  dialogConfig = DIALOG_CONFIGS.MEDIUM;

  onHide() {
    this.visibleChange.emit(false);
  }

  getPlatformIconClass(platform: ProviderPlatform): string {
    switch (platform) {
      case ProviderPlatform.GEMINI_OAUTH:
      case ProviderPlatform.GEMINI_APIKEY:
        return 'text-primary';
      case ProviderPlatform.CLAUDE_OAUTH:
      case ProviderPlatform.CLAUDE_APIKEY:
        return 'text-orange-600 dark:text-orange-500';
      case ProviderPlatform.OPENAI_OAUTH:
      case ProviderPlatform.OPENAI_APIKEY:
        return 'text-emerald-600 dark:text-emerald-500';
      default:
        return 'text-muted-color';
    }
  }

  getPlatformLabel(platform: ProviderPlatform): string {
    return PROVIDER_PLATFORM_LABELS[platform] || platform;
  }

  isOAuthPlatform(platform: ProviderPlatform): boolean {
    return (
      platform === ProviderPlatform.GEMINI_OAUTH ||
      platform === ProviderPlatform.ANTIGRAVITY ||
      platform === ProviderPlatform.CLAUDE_OAUTH ||
      platform === ProviderPlatform.OPENAI_OAUTH
    );
  }
}
