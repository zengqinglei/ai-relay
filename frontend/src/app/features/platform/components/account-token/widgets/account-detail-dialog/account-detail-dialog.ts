import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';

import { PlatformIcon } from '../../../../../../shared/components/platform-icon/platform-icon';
import { DIALOG_CONFIGS } from '../../../../../../shared/constants/dialog-config.constants';
import { AUTH_METHOD_LABELS, PROVIDER_LABELS, getProviderAuthLabel } from '../../../../../../shared/constants/provider.constants';
import { AuthMethod } from '../../../../../../shared/models/auth-method.enum';
import { Provider } from '../../../../../../shared/models/provider.enum';
import { AccountTokenOutputDto, RateLimitScope } from '../../../../models/account-token.dto';

@Component({
  selector: 'app-account-detail-dialog',
  standalone: true,
  imports: [CommonModule, DialogModule, ButtonModule, TooltipModule, PlatformIcon],
  templateUrl: './account-detail-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AccountDetailDialogComponent {
  readonly RateLimitScope = RateLimitScope;

  @Input() visible = false;
  @Input() account: AccountTokenOutputDto | null = null;
  @Output() readonly visibleChange = new EventEmitter<boolean>();

  dialogConfig = DIALOG_CONFIGS.MEDIUM;

  onHide() {
    this.visibleChange.emit(false);
  }

  getProviderIconClass(provider: Provider): string {
    switch (provider) {
      case Provider.Gemini:
        return 'text-primary';
      case Provider.Claude:
        return 'text-orange-600 dark:text-orange-500';
      case Provider.OpenAI:
        return 'text-emerald-600 dark:text-emerald-500';
      case Provider.Antigravity:
        return 'text-purple-600 dark:text-purple-400';
      default:
        return 'text-muted-color';
    }
  }

  getProviderLabel(provider: Provider): string {
    return PROVIDER_LABELS[provider] || provider;
  }

  getAuthMethodLabel(authMethod: AuthMethod): string {
    return AUTH_METHOD_LABELS[authMethod] || authMethod;
  }

  getProviderAuthLabel(provider: Provider, authMethod: AuthMethod): string {
    return getProviderAuthLabel(provider, authMethod);
  }

  getRateLimitScopeLabel(scope: RateLimitScope): string {
    return scope === RateLimitScope.Model ? '按模型' : '按账户';
  }

  getLimitedModelLabel(modelKey: string, displayName?: string): string {
    return displayName || modelKey;
  }

  isOAuth(authMethod: AuthMethod): boolean {
    return authMethod === AuthMethod.OAuth;
  }
}
