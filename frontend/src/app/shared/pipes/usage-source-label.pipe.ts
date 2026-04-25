import { Pipe, PipeTransform } from '@angular/core';

import { USAGE_SOURCE_LABELS } from '../constants/usage-source.constants';
import { UsageSource } from '../models/usage-source.enum';

@Pipe({
  name: 'usageSourceLabel',
  standalone: true
})
export class UsageSourceLabelPipe implements PipeTransform {
  transform(value: UsageSource | string): string {
    const source = value as UsageSource;
    return USAGE_SOURCE_LABELS[source] || value;
  }
}
