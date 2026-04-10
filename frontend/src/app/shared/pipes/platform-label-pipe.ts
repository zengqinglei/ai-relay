import { Pipe, PipeTransform } from '@angular/core';

import { PROVIDER_LABELS } from '../constants/provider.constants';
import { Provider } from '../models/provider.enum';

@Pipe({
  name: 'providerLabel',
  standalone: true
})
export class ProviderLabelPipe implements PipeTransform {
  transform(value: Provider | string): string {
    const provider = value as Provider;
    return PROVIDER_LABELS[provider] || value;
  }
}
