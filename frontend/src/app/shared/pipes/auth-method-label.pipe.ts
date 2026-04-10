import { Pipe, PipeTransform } from '@angular/core';
import { AuthMethod } from '../models/auth-method.enum';

@Pipe({
  name: 'authMethodLabel',
  standalone: true
})
export class AuthMethodLabelPipe implements PipeTransform {
  transform(value: AuthMethod | number | string | undefined | null): string {
    if (value === undefined || value === null) return '-';
    
    // Handle both enum values and numbers from mock/API
    const method = typeof value === 'string' ? value : Number(value);
    
    switch (method) {
      case AuthMethod.OAuth:
      case 0:
        return 'OAuth';
      case AuthMethod.ApiKey:
      case 1:
        return 'API Key';
      default:
        return '未知方式';
    }
  }
}
