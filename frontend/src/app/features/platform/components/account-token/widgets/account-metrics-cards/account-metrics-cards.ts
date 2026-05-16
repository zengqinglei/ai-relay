import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CardModule } from 'primeng/card';

import { formatNumber, formatTokenCount } from '../../../../../../shared/utils/format.utils';
import { AccountTokenMetricsOutputDto } from '../../../../models/account-token.dto';

@Component({
  selector: 'app-account-metrics-cards',
  standalone: true,
  imports: [CommonModule, CardModule, DecimalPipe],
  templateUrl: './account-metrics-cards.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'contents' } // Use contents to let grid layout apply directly to cards
})
export class AccountMetricsCards {
  metrics = input.required<AccountTokenMetricsOutputDto>();

  protected readonly formatNumber = formatNumber;
  protected readonly formatTokenCount = formatTokenCount;

  protected totalTokensToday(): number {
    const metrics = this.metrics();
    return metrics.totalInputTokensToday + metrics.totalOutputTokensToday;
  }
}
