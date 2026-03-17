import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-register',
  standalone: true,
  template: `<p>register works!</p>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Register {}
