import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';

import { DIALOG_CONFIGS } from '../../../../../../shared/constants/dialog-config.constants';
import { CreateProviderGroupInputDto, ProviderGroupOutputDto, UpdateProviderGroupInputDto } from '../../../../models/provider-group.dto';

type ProviderGroupEditFormModel = {
  name: string;
  description?: string;
  enableStickySession: boolean;
  stickySessionExpirationHours: number;
  rateMultiplier: number;
};

@Component({
  selector: 'app-group-edit-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ButtonModule, InputTextModule, InputNumberModule, TextareaModule, ToggleSwitchModule],
  templateUrl: './group-edit-dialog.html'
})
export class GroupEditDialogComponent {
  @Input() visible = false;
  @Input() loading = false;
  @Input() saving = false;
  @Input() readonly = false;
  @Input() set group(value: ProviderGroupOutputDto | null) {
    this.currentGroup.set(value);
    this.isEditMode.set(!!value);
    this.formModel.set(value ? this.mapToFormModel(value) : this.createEmptyGroup());
  }

  @Output() readonly visibleChange = new EventEmitter<boolean>();
  @Output() readonly saved = new EventEmitter<CreateProviderGroupInputDto | UpdateProviderGroupInputDto>();

  currentGroup = signal<ProviderGroupOutputDto | null>(null);
  isEditMode = signal(false);
  formModel = signal<ProviderGroupEditFormModel>(this.createEmptyGroup());
  dialogConfig = DIALOG_CONFIGS.SMALL;

  readonly isDefaultGroup = computed(() => this.currentGroup()?.isDefault ?? false);
  readonly isNameReadonly = computed(() => this.readonly || this.isDefaultGroup());

  createEmptyGroup(): ProviderGroupEditFormModel {
    return {
      name: '',
      description: '',
      rateMultiplier: 1,
      enableStickySession: true,
      stickySessionExpirationHours: 1
    };
  }

  private mapToFormModel(group: ProviderGroupOutputDto): ProviderGroupEditFormModel {
    return {
      name: group.name,
      description: group.description,
      rateMultiplier: group.rateMultiplier,
      enableStickySession: group.enableStickySession,
      stickySessionExpirationHours: group.stickySessionExpirationHours
    };
  }

  onHide() {
    this.visible = false;
    this.visibleChange.emit(false);
    this.currentGroup.set(null);
    this.formModel.set(this.createEmptyGroup());
    this.isEditMode.set(false);
  }

  isValid(): boolean {
    const model = this.formModel();

    if (!model.name || model.name.length > 256) {
      return false;
    }

    if (model.description && model.description.length > 1000) {
      return false;
    }

    if (!model.rateMultiplier || model.rateMultiplier < 0.01 || model.rateMultiplier > 100) {
      return false;
    }

    if (model.enableStickySession) {
      if (!model.stickySessionExpirationHours || model.stickySessionExpirationHours < 1 || model.stickySessionExpirationHours > 8760) {
        return false;
      }
    }

    return true;
  }

  save() {
    if (!this.isValid()) {
      return;
    }

    this.saved.emit({ ...this.formModel() });
  }
}
