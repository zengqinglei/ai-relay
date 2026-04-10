import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { DividerModule } from 'primeng/divider';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';

import { DIALOG_CONFIGS } from '../../../../../../shared/constants/dialog-config.constants';
import { ROUTE_PROFILE_FULL_LABELS, ROUTE_PROFILE_LABELS } from '../../../../../../shared/constants/route-profile.constants';
import { RouteProfile } from '../../../../../../shared/models/route-profile.enum';
import { ProviderGroupOutputDto } from '../../../../models/provider-group.dto';
import { ApiKeyBindGroupInputDto, ApiKeyOutputDto } from '../../../../models/subscription.dto';
import { ProviderGroupService } from '../../../../services/provider-group-service';

@Component({
  selector: 'app-subscription-edit-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    ButtonModule,
    InputTextModule,
    TextareaModule,
    DatePickerModule,
    SelectModule,
    DividerModule,
    ProgressSpinnerModule,
    TooltipModule
  ],
  templateUrl: './subscription-edit-dialog.html'
})
export class SubscriptionEditDialogComponent {
  @Input() visible = false;
  @Input() loading = false;
  @Input() saving = false;
  @Input() set subscription(value: ApiKeyOutputDto | null) {
    if (value) {
      this.isEditMode.set(true);
      this.formModel.set({
        name: value.name,
        description: value.description,
        expiresAt: value.expiresAt,
        customSecret: '',
        bindings: []
      });
      this.expiryDate = value.expiresAt ? new Date(value.expiresAt) : null;
      this.formModel.update(m => {
        m.bindings = [...(value.bindings || [])].sort((a, b) => a.priority - b.priority).map(b => ({
          providerGroupId: b.providerGroupId,
          priority: b.priority
        }));
        return m;
      });
    } else {
      this.isEditMode.set(false);
      this.formModel.set(this.createEmptyModel());
      this.expiryDate = null;
    }
  }
  @Output() readonly visibleChange = new EventEmitter<boolean>();
  @Output() readonly saved = new EventEmitter<any>();

  private groupService = inject(ProviderGroupService);

  isEditMode = signal(false);
  formModel = signal<any>(this.createEmptyModel());
  expiryDate: Date | null = null;
  maxPools = signal(5);
  allGroups = signal<ProviderGroupOutputDto[]>([]);
  dialogConfig = DIALOG_CONFIGS.SMALL;

  constructor() {
    this.groupService.getGroups({ offset: 0, limit: 1000 }).subscribe(result => this.allGroups.set(result.items));
  }

  createEmptyModel() {
    return {
      name: '',
      description: '',
      expiresAt: null,
      customSecret: '',
      bindings: []
    };
  }

  addFallbackPool() {
    this.formModel.update(m => {
      if (m.bindings.length < this.maxPools()) {
        m.bindings.push({ providerGroupId: null, priority: m.bindings.length + 1 });
      }
      return m;
    });
  }

  removePool(index: number) {
    this.formModel.update(m => {
      m.bindings.splice(index, 1);
      return m;
    });
  }

  getAvailableGroups(currentIndex: number): ProviderGroupOutputDto[] {
    const currentId = this.formModel().bindings[currentIndex]?.providerGroupId;
    const selectedIds = new Set(
      this.formModel().bindings
        .filter((_: { providerGroupId: string | null }, index: number) => index !== currentIndex)
        .map((binding: { providerGroupId: string | null }) => binding.providerGroupId)
        .filter((id: string | null): id is string => !!id)
    );

    return this.allGroups().filter(group => group.id === currentId || !selectedIds.has(group.id));
  }

  getRouteProfileLabel(profile: RouteProfile): string {
    return ROUTE_PROFILE_LABELS[profile] || profile;
  }

  getRouteProfileFullLabel(profile: RouteProfile): string {
    return ROUTE_PROFILE_FULL_LABELS[profile] || profile;
  }

  onHide() {
    this.visible = false;
    this.visibleChange.emit(false);
    this.formModel.set(this.createEmptyModel());
    this.expiryDate = null;
  }

  isValid(): boolean {
    if (!this.formModel().name) return false;
    if (this.formModel().customSecret && !this.isSecretValid()) {
      return false;
    }
    return true;
  }

  isSecretValid(): boolean {
    const secret = this.formModel().customSecret;
    if (!secret) return true;
    const regex = /^(?=.*[0-9])(?=.*[a-zA-Z])[a-zA-Z0-9_-]{6,48}$/;
    return regex.test(secret);
  }

  save() {
    if (this.isValid()) {
      const bindings: ApiKeyBindGroupInputDto[] = this.formModel().bindings
        .filter((b: any) => !!b.providerGroupId)
        .map((b: any, index: number) => ({
          priority: index + 1,
          providerGroupId: b.providerGroupId
        }));

      const payload = {
        ...this.formModel(),
        expiresAt: this.expiryDate ? this.expiryDate.toISOString() : null,
        bindings: bindings
      };

      this.saved.emit(payload);
    }
  }
}
