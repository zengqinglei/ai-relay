import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { finalize } from 'rxjs';

import { DIALOG_CONFIGS } from '../../../../../../shared/constants/dialog-config.constants';
import { UserOutputDto } from '../../../../../account/models/account.dto';
import { CreateProviderGroupInputDto, ProviderGroupOutputDto, UpdateProviderGroupInputDto } from '../../../../models/provider-group.dto';
import { PlatformUserService } from '../../../../services/platform-user-service';
import { DialogLoadingComponent } from '../../../../../../shared/components/dialog-loading/dialog-loading';

type ProviderGroupEditFormModel = {
  name: string;
  description?: string;
  assignedUserIds: string[];
  enableStickySession: boolean;
  stickySessionExpirationHours: number;
  rateMultiplier: number;
};

@Component({
  selector: 'app-group-edit-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ButtonModule, InputTextModule, InputNumberModule, TextareaModule, ToggleSwitchModule, MultiSelectModule, DialogLoadingComponent],
  templateUrl: './group-edit-dialog.html'
})
export class GroupEditDialogComponent implements OnChanges {
  private readonly userService = inject(PlatformUserService);

  @Input() visible = false;
  @Input() loading = false;
  @Input() saving = false;
  @Input() readonly = false;
  @Input() set group(value: ProviderGroupOutputDto | null) {
    this.currentGroup.set(value);
    this.isEditMode.set(!!value);
    this.formModel.set(value ? this.mapToFormModel(value) : this.createEmptyGroup());
    this.selectedAssignedUsers.set(
      (value?.assignedUserIds ?? []).map((id, index) => ({
        id,
        username: value?.assignedUsernames?.[index] ?? '未命名用户',
        email: '',
        isActive: true,
        creationTime: '',
        roles: []
      }))
    );
  }

  @Output() readonly visibleChange = new EventEmitter<boolean>();
  @Output() readonly saved = new EventEmitter<CreateProviderGroupInputDto | UpdateProviderGroupInputDto>();

  currentGroup = signal<ProviderGroupOutputDto | null>(null);
  isEditMode = signal(false);
  formModel = signal<ProviderGroupEditFormModel>(this.createEmptyGroup());
  userOptions = signal<UserOutputDto[]>([]);
  selectedAssignedUsers = signal<UserOutputDto[]>([]);
  userOptionsLoading = signal(false);
  dialogConfig = DIALOG_CONFIGS.SMALL;

  readonly isDefaultGroup = computed(() => this.currentGroup()?.isDefault ?? false);
  readonly isNameReadonly = computed(() => this.readonly || this.isDefaultGroup());

  createEmptyGroup(): ProviderGroupEditFormModel {
    return {
      name: '',
      description: '',
      assignedUserIds: [],
      rateMultiplier: 1,
      enableStickySession: true,
      stickySessionExpirationHours: 1
    };
  }

  private mapToFormModel(group: ProviderGroupOutputDto): ProviderGroupEditFormModel {
    return {
      name: group.name,
      description: group.description,
      assignedUserIds: group.assignedUserIds ?? [],
      rateMultiplier: group.rateMultiplier,
      enableStickySession: group.enableStickySession,
      stickySessionExpirationHours: group.stickySessionExpirationHours
    };
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['visible']?.currentValue) {
      this.loadUserOptions();
    }
  }

  onHide() {
    this.visible = false;
    this.visibleChange.emit(false);
    this.currentGroup.set(null);
    this.formModel.set(this.createEmptyGroup());
    this.selectedAssignedUsers.set([]);
    this.userOptions.set([]);
    this.userOptionsLoading.set(false);
    this.isEditMode.set(false);
  }

  onAssignedUsersChange(users: UserOutputDto[] | null) {
    const nextUsers = users ?? [];
    this.selectedAssignedUsers.set(nextUsers);
    this.formModel.update(model => ({
      ...model,
      assignedUserIds: nextUsers.map(user => user.id)
    }));
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

    this.saved.emit({ ...this.formModel(), assignedUserIds: this.selectedAssignedUsers().map(user => user.id) });
  }

  private loadUserOptions() {
    if (this.userOptionsLoading()) {
      return;
    }

    this.userOptionsLoading.set(true);
    this.userService
      .getUsers({ offset: 0, limit: 200, isActive: true })
      .pipe(finalize(() => this.userOptionsLoading.set(false)))
      .subscribe(result => {
        const loadedUsers = result.items;
        const fallbackSelectedUsers = this.selectedAssignedUsers().filter(
          selectedUser => !loadedUsers.some(user => user.id === selectedUser.id)
        );

        this.userOptions.set([...fallbackSelectedUsers, ...loadedUsers]);
      });
  }
}
