import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { DividerModule } from 'primeng/divider';
import { InputTextModule } from 'primeng/inputtext';
import { Popover, PopoverModule } from 'primeng/popover';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';

import { DIALOG_CONFIGS } from '../../../../../../shared/constants/dialog-config.constants';
import { ROUTE_PROFILE_FULL_LABELS, ROUTE_PROFILE_LABELS } from '../../../../../../shared/constants/route-profile.constants';
import { RouteProfile } from '../../../../../../shared/models/route-profile.enum';
import { ProviderGroupOutputDto } from '../../../../models/provider-group.dto';
import { ApiKeyBindGroupInputDto, ApiKeyOutputDto } from '../../../../models/subscription.dto';
import { ProviderGroupService } from '../../../../services/provider-group-service';
import {
  RelationPopoverContentComponent,
  RelationPopoverItem
} from '../../../shared/widgets/relation-popover-content/relation-popover-content';

type SubscriptionBindingFormItem = {
  providerGroupId: string;
};

type SubscriptionDialogSavePayload = {
  name: string;
  description?: string;
  expiresAt?: string | null;
  customSecret: string;
  bindings: ApiKeyBindGroupInputDto[];
};

type SubscriptionEditFormModel = {
  name: string;
  description?: string;
  expiresAt?: string | null;
  customSecret: string;
  bindings: SubscriptionBindingFormItem[];
};

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
    DividerModule,
    ProgressSpinnerModule,
    TooltipModule,
    AutoCompleteModule,
    TagModule,
    PopoverModule,
    RelationPopoverContentComponent
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
        bindings: [...(value.bindings || [])]
          .sort((a, b) => a.priority - b.priority)
          .map(binding => ({ providerGroupId: binding.providerGroupId }))
      });
      this.expiryDate = value.expiresAt ? new Date(value.expiresAt) : null;
    } else {
      this.isEditMode.set(false);
      this.formModel.set(this.createEmptyModel());
      this.expiryDate = null;
    }
  }
  @Output() readonly visibleChange = new EventEmitter<boolean>();
  @Output() readonly saved = new EventEmitter<SubscriptionDialogSavePayload>();

  private groupService = inject(ProviderGroupService);

  isEditMode = signal(false);
  formModel = signal<SubscriptionEditFormModel>(this.createEmptyModel());
  expiryDate: Date | null = null;
  maxPools = signal(5);
  allGroups = signal<ProviderGroupOutputDto[]>([]);
  filteredGroups = signal<ProviderGroupOutputDto[]>([]);
  selectedGroupToAdd: ProviderGroupOutputDto | null = null;
  dialogConfig = DIALOG_CONFIGS.SMALL;
  visibleRouteCount = signal(1);
  activeRouteProfiles = signal<RouteProfile[]>([]);

  constructor() {
    this.groupService.getGroups({ offset: 0, limit: 1000 }).subscribe(result => this.allGroups.set(result.items));
  }

  createEmptyModel(): SubscriptionEditFormModel {
    return {
      name: '',
      description: '',
      expiresAt: null,
      customSecret: '',
      bindings: []
    };
  }

  searchGroups(event: { query: string }) {
    const query = event.query.trim().toLowerCase();
    const selectedIds = new Set(this.formModel().bindings.map(binding => binding.providerGroupId));

    this.filteredGroups.set(
      this.allGroups().filter(group => {
        if (selectedIds.has(group.id)) {
          return false;
        }

        if (!query) {
          return true;
        }

        return group.name.toLowerCase().includes(query) || (group.description ?? '').toLowerCase().includes(query);
      })
    );
  }

  addSelectedGroup() {
    const group = this.selectedGroupToAdd;
    if (!group) {
      return;
    }

    this.formModel.update(model => {
      if (model.bindings.length >= this.maxPools() || model.bindings.some(binding => binding.providerGroupId === group.id)) {
        return model;
      }

      return {
        ...model,
        bindings: [...model.bindings, { providerGroupId: group.id }]
      };
    });

    this.selectedGroupToAdd = null;
  }

  removePool(index: number) {
    this.formModel.update(model => ({
      ...model,
      bindings: model.bindings.filter((_, currentIndex) => currentIndex !== index)
    }));
  }

  movePool(index: number, direction: -1 | 1) {
    this.formModel.update(model => {
      const nextIndex = index + direction;
      if (nextIndex < 0 || nextIndex >= model.bindings.length) {
        return model;
      }

      const bindings = [...model.bindings];
      [bindings[index], bindings[nextIndex]] = [bindings[nextIndex], bindings[index]];
      return { ...model, bindings };
    });
  }

  getGroup(groupId: string): ProviderGroupOutputDto | undefined {
    return this.allGroups().find(group => group.id === groupId);
  }

  getBindingLabel(index: number): string {
    return index === 0 ? '主分组' : `Fallback ${index}`;
  }

  getVisibleRouteProfiles(group: ProviderGroupOutputDto | undefined): RouteProfile[] {
    return group?.supportedRouteProfiles?.slice(0, this.visibleRouteCount()) ?? [];
  }

  getHiddenRouteProfiles(group: ProviderGroupOutputDto | undefined): RouteProfile[] {
    return group?.supportedRouteProfiles?.slice(this.visibleRouteCount()) ?? [];
  }

  openRouteProfilesPopover(event: Event, popover: Popover, profiles: RouteProfile[]) {
    this.activeRouteProfiles.set(profiles);
    popover.toggle(event);
  }

  getRoutePopoverItems(): RelationPopoverItem[] {
    return this.activeRouteProfiles().map(profile => ({
      id: profile,
      leftText: this.getRouteProfileLabel(profile),
      rightText: this.getRouteProfilePath(profile)
    }));
  }

  getStickySessionSummary(group: ProviderGroupOutputDto | undefined): string {
    if (!group?.enableStickySession) {
      return '未开启';
    }

    return `${group.stickySessionExpirationHours}h`;
  }

  getRouteProfileLabel(profile: RouteProfile): string {
    return ROUTE_PROFILE_LABELS[profile] || profile;
  }

  getRouteProfileFullLabel(profile: RouteProfile): string {
    return ROUTE_PROFILE_FULL_LABELS[profile] || profile;
  }

  getRouteProfilePath(profile: RouteProfile): string {
    const fullLabel = this.getRouteProfileFullLabel(profile);
    const match = /\(([^)]+)\)$/.exec(fullLabel);
    return match?.[1] ?? fullLabel;
  }

  onHide() {
    this.visible = false;
    this.visibleChange.emit(false);
    this.formModel.set(this.createEmptyModel());
    this.expiryDate = null;
    this.selectedGroupToAdd = null;
    this.activeRouteProfiles.set([]);
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
    if (!this.isValid()) {
      return;
    }

    const bindings: ApiKeyBindGroupInputDto[] = this.formModel().bindings.map((binding, index) => ({
      priority: index + 1,
      providerGroupId: binding.providerGroupId
    }));

    const payload = {
      ...this.formModel(),
      expiresAt: this.expiryDate ? this.expiryDate.toISOString() : null,
      bindings
    };

    this.saved.emit(payload);
  }
}

