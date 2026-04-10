import { Pipe, PipeTransform } from '@angular/core';

import { ROUTE_PROFILE_LABELS } from '../constants/route-profile.constants';
import { RouteProfile } from '../models/route-profile.enum';

@Pipe({
  name: 'routeProfileLabel',
  standalone: true
})
export class RouteProfileLabelPipe implements PipeTransform {
  transform(value: RouteProfile | string): string {
    const routeProfile = value as RouteProfile;
    return ROUTE_PROFILE_LABELS[routeProfile] || value;
  }
}
