import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ProgressSpinnerModule } from 'primeng/progressspinner';

@Component({
  selector: 'app-dialog-loading',
  standalone: true,
  imports: [ProgressSpinnerModule],
  template: `
    <div class="flex min-h-64 flex-col items-center justify-center gap-4 py-10 text-center">
      <p-progressSpinner
        ariaLabel="loading"
        strokeWidth="4"
        styleClass="h-10 w-10"
      />
      <div class="text-sm text-muted-color">{{ text() }}</div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DialogLoadingComponent {
  readonly text = input('正在加载...');
}
