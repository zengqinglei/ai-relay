import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type RelationPopoverDisplayMode = 'route-details' | 'binding-summary';

export interface RelationPopoverItem {
  id: string;
  leftText: string;
  rightText: string;
  isWarning?: boolean;
}

@Component({
  selector: 'app-relation-popover-content',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './relation-popover-content.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RelationPopoverContentComponent {
  items = input.required<RelationPopoverItem[]>();
  mode = input<RelationPopoverDisplayMode>('route-details');
}
