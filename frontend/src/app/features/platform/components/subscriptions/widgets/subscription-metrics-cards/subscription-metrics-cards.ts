import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CardModule } from 'primeng/card';
import { TooltipModule } from 'primeng/tooltip';

import { SubscriptionMetricsOutputDto } from '../../../../models/subscription.dto';

@Component({
  selector: 'app-subscription-metrics-cards',
  standalone: true,
  imports: [CommonModule, CardModule, TooltipModule],
  templateUrl: './subscription-metrics-cards.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SubscriptionMetricsCards {
  metrics = input.required<SubscriptionMetricsOutputDto>();
}
