import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-default-footer',
  standalone: true,
  imports: [],
  templateUrl: './default-footer.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DefaultFooter {}
