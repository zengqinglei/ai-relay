import { ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RadioButtonModule } from 'primeng/radiobutton';

export type UserScopeOption = {
  label: string;
  value: string;
};

@Component({
  selector: 'app-user-scope-filter',
  standalone: true,
  imports: [FormsModule, RadioButtonModule],
  templateUrl: './user-scope-filter.html',
  styleUrl: './user-scope-filter.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserScopeFilterComponent {
  readonly label = input('查看范围');
  readonly showLabel = input(true);
  readonly name = input('user-scope');
  readonly selected = model('all');
  readonly options = input<UserScopeOption[]>([]);
  readonly layout = input<'inline' | 'stacked'>('inline');

  readonly isStacked = computed(() => this.layout() === 'stacked');

  getOptionInputId(value: string): string {
    return `${this.name()}-${value}`;
  }

  onSelectedChange(value: string) {
    this.selected.set(value);
  }
}
