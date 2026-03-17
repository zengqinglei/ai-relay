import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-logo',
  templateUrl: './logo.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LogoComponent {}
