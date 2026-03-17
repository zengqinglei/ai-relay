import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-workspace-placeholder',
  standalone: true,
  templateUrl: './workspace-placeholder.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WorkspacePlaceholder {}
