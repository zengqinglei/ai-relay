import { Pipe, PipeTransform } from '@angular/core';

import { PROVIDER_PLATFORM_LABELS } from '../constants/provider-platform.constants';
import { ProviderPlatform } from '../models/provider-platform.enum';

@Pipe({
  name: 'platformLabel',
  standalone: true
})
export class PlatformLabelPipe implements PipeTransform {
  transform(value: ProviderPlatform | string): string {
    const platform = value as ProviderPlatform;
    return PROVIDER_PLATFORM_LABELS[platform] || value;
  }
}
