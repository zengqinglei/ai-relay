import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-settings',
  standalone: true,
  templateUrl: './settings.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Settings {}
