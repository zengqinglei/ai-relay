import { Pipe, PipeTransform } from '@angular/core';

import { GroupSchedulingStrategy } from '../../features/platform/models/provider-group.dto';

@Pipe({
  name: 'schedulingStrategySeverity',
  standalone: true
})
export class SchedulingStrategySeverityPipe implements PipeTransform {
  transform(value: GroupSchedulingStrategy | string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' | undefined {
    switch (value) {
      case GroupSchedulingStrategy.WeightedRandom:
        return 'success';
      case GroupSchedulingStrategy.AdaptiveBalanced:
        return 'warn';
      case GroupSchedulingStrategy.Priority:
        return 'danger';
      case GroupSchedulingStrategy.QuotaPriority:
        return 'info';
      default:
        return 'info';
    }
  }
}
