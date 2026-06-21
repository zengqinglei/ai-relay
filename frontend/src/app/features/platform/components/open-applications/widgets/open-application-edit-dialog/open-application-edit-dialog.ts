import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, input, model, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DividerModule } from 'primeng/divider';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';

import { DialogLoadingComponent } from '../../../../../../shared/components/dialog-loading/dialog-loading';
import { DIALOG_CONFIGS } from '../../../../../../shared/constants/dialog-config.constants';
import {
  CreateOpenApplicationInputDto,
  OpenApplicationClientType,
  OpenApplicationConsentType,
  OpenApplicationOutputDto,
  OpenApplicationType,
  UpdateOpenApplicationInputDto
} from '../../../../models/open-application.dto';

type OpenApplicationTemplate = 'web' | 'desktop' | 'service';

type OpenApplicationEditFormModel = {
  clientId: string;
  displayName?: string;
  applicationType: OpenApplicationType;
  clientType: OpenApplicationClientType;
  consentType: OpenApplicationConsentType;
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  permissions: string[];
  requirements: string[];
  redirectUriInput: string;
  postLogoutRedirectUriInput: string;
};

const authorizationCodePermissions = [
  'ept:authorization',
  'ept:end_session',
  'ept:token',
  'gt:authorization_code',
  'gt:refresh_token',
  'rst:code',
  'scp:openid',
  'scp:profile',
  'scp:email',
  'scp:roles',
  'scp:offline_access'
];

@Component({
  selector: 'app-open-application-edit-dialog',
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    ButtonModule,
    InputTextModule,
    TextareaModule,
    SelectModule,
    MultiSelectModule,
    TagModule,
    DividerModule,
    TooltipModule,
    DialogLoadingComponent
  ],
  templateUrl: './open-application-edit-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OpenApplicationEditDialogComponent {
  visible = model(false);
  loading = input(false);
  saving = input(false);
  application = input<OpenApplicationOutputDto | null>(null);
  readonly saved = output<CreateOpenApplicationInputDto | UpdateOpenApplicationInputDto>();

  formModel = signal<OpenApplicationEditFormModel>(this.createEmptyModel());
  dialogConfig = DIALOG_CONFIGS.MEDIUM;
  selectedTemplate = signal<OpenApplicationTemplate | null>(null);

  isEditMode = computed(() => !!this.application());
  isTemplateLocked = computed(() => !!this.selectedTemplate() && !this.isEditMode());
  isConfidentialClient = computed(() => this.formModel().clientType === 'confidential');
  isServiceType = computed(() => this.formModel().applicationType === 'service');
  isValid = computed(() => {
    const model = this.formModel();
    if (!this.isEditMode() && !model.clientId.trim()) {
      return false;
    }
    if (!model.applicationType || !model.clientType || !model.consentType) {
      return false;
    }
    if ((model.applicationType === 'native' || model.clientType === 'public') && !model.requirements.includes('ft:pkce')) {
      return false;
    }
    return true;
  });

  templateOptions = [
    { label: 'Web PKCE 客户端', value: 'web' },
    { label: '桌面端 PKCE 客户端', value: 'desktop' },
    { label: '服务端机密客户端', value: 'service' }
  ];

  applicationTypeOptions = [
    { label: 'Web', value: 'web' },
    { label: '桌面/原生', value: 'native' },
    { label: '服务端', value: 'service' }
  ];

  clientTypeOptions = [
    { label: 'Public', value: 'public' },
    { label: 'Confidential', value: 'confidential' }
  ];

  consentTypeOptions = [
    { label: '隐式同意', value: 'implicit' },
    { label: '显式同意', value: 'explicit' },
    { label: '外部同意', value: 'external' },
    { label: '系统同意', value: 'systematic' }
  ];

  permissionOptions = [
    { label: '授权端点', value: 'ept:authorization', group: 'Endpoints' },
    { label: 'Token 端点', value: 'ept:token', group: 'Endpoints' },
    { label: '登出端点', value: 'ept:end_session', group: 'Endpoints' },
    { label: '授权码', value: 'gt:authorization_code', group: 'Grant Types' },
    { label: '刷新令牌', value: 'gt:refresh_token', group: 'Grant Types' },
    { label: '客户端凭据', value: 'gt:client_credentials', group: 'Grant Types' },
    { label: 'Code 响应', value: 'rst:code', group: 'Response Types' },
    { label: 'openid', value: 'scp:openid', group: 'Scopes' },
    { label: 'profile', value: 'scp:profile', group: 'Scopes' },
    { label: 'email', value: 'scp:email', group: 'Scopes' },
    { label: 'roles', value: 'scp:roles', group: 'Scopes' },
    { label: 'offline_access', value: 'scp:offline_access', group: 'Scopes' }
  ];

  requirementOptions = [{ label: '强制 PKCE', value: 'ft:pkce' }];

  readonly permissionLabels: Record<string, string> = {
    'ept:authorization': '授权端点',
    'ept:token': 'Token 端点',
    'ept:end_session': '登出端点',
    'gt:authorization_code': '授权码流程',
    'gt:refresh_token': '刷新令牌',
    'gt:client_credentials': '客户端凭据',
    'rst:code': 'Code 响应',
    'scp:openid': '身份标识',
    'scp:profile': '个人资料',
    'scp:email': '邮箱',
    'scp:roles': '角色',
    'scp:offline_access': '离线访问'
  };

  readonly applicationTypeLabels: Record<string, string> = {
    'web': 'Web',
    'native': '桌面/原生',
    'service': '服务端'
  };

  readonly clientTypeLabels: Record<string, string> = {
    'public': 'Public（公开）',
    'confidential': 'Confidential（机密）'
  };

  readonly consentTypeLabels: Record<string, string> = {
    'implicit': '隐式同意',
    'explicit': '显式同意',
    'external': '外部同意',
    'systematic': '系统同意'
  };

  getPermissionLabel(value: string): string {
    return this.permissionLabels[value] ?? value;
  }

  constructor() {
    effect(() => {
      const application = this.application();
      if (application) {
        this.formModel.set({
          clientId: application.clientId,
          displayName: application.displayName,
          applicationType: application.applicationType,
          clientType: application.clientType,
          consentType: application.consentType,
          redirectUris: [...application.redirectUris],
          postLogoutRedirectUris: [...application.postLogoutRedirectUris],
          permissions: [...application.permissions],
          requirements: [...application.requirements],
          redirectUriInput: '',
          postLogoutRedirectUriInput: ''
        });
      } else {
        this.formModel.set(this.createEmptyModel());
      }
    });
  }

  createEmptyModel(): OpenApplicationEditFormModel {
    return {
      clientId: '',
      displayName: '',
      applicationType: 'web',
      clientType: 'public',
      consentType: 'explicit',
      redirectUris: [],
      postLogoutRedirectUris: [],
      permissions: [...authorizationCodePermissions],
      requirements: ['ft:pkce'],
      redirectUriInput: '',
      postLogoutRedirectUriInput: ''
    };
  }

  applyTemplate(template: OpenApplicationTemplate) {
    this.selectedTemplate.set(template);
    if (template === 'desktop') {
      this.formModel.update(model => ({
        ...model,
        clientId: model.clientId || 'ai-relay-desktop',
        displayName: model.displayName || 'AiRelay Desktop',
        applicationType: 'native',
        clientType: 'public',
        consentType: 'explicit',
        redirectUris: ['ai-relay-desktop://oauth/callback'],
        postLogoutRedirectUris: ['ai-relay-desktop://oauth/logout-callback'],
        permissions: [...authorizationCodePermissions],
        requirements: ['ft:pkce']
      }));
      return;
    }

    if (template === 'service') {
      this.formModel.update(model => ({
        ...model,
        applicationType: 'service',
        clientType: 'confidential',
        consentType: 'systematic',
        redirectUris: [],
        postLogoutRedirectUris: [],
        permissions: ['ept:token', 'gt:client_credentials'],
        requirements: []
      }));
      return;
    }

    this.formModel.update(model => ({
      ...model,
      applicationType: 'web',
      clientType: 'public',
      consentType: 'explicit',
      permissions: [...authorizationCodePermissions],
      requirements: ['ft:pkce']
    }));
  }

  onClientTypeChange() {
    if (this.formModel().clientType === 'public') {
      this.formModel.update(model => ({ ...model, requirements: this.ensurePkce(model.requirements) }));
    }
  }

  onClientTypeSelect(clientType: OpenApplicationClientType) {
    this.formModel.update(model => ({
      ...model,
      clientType,
      requirements: clientType === 'public' ? this.ensurePkce(model.requirements) : model.requirements.filter(r => r !== 'ft:pkce')
    }));
  }

  addRedirectUri() {
    this.addUri('redirectUris', 'redirectUriInput');
  }

  addPostLogoutRedirectUri() {
    this.addUri('postLogoutRedirectUris', 'postLogoutRedirectUriInput');
  }

  removeRedirectUri(uri: string) {
    this.formModel.update(model => ({ ...model, redirectUris: model.redirectUris.filter(item => item !== uri) }));
  }

  removePostLogoutRedirectUri(uri: string) {
    this.formModel.update(model => ({ ...model, postLogoutRedirectUris: model.postLogoutRedirectUris.filter(item => item !== uri) }));
  }

  isRedirectUriInputInvalid() {
    const value = this.formModel().redirectUriInput.trim();
    return !!value && !this.isValidRedirectUri(value);
  }

  isPostLogoutRedirectUriInputInvalid() {
    const value = this.formModel().postLogoutRedirectUriInput.trim();
    return !!value && !this.isValidRedirectUri(value);
  }

  onHide() {
    this.visible.set(false);
    this.formModel.set(this.createEmptyModel());
    this.selectedTemplate.set(null);
  }

  save() {
    if (!this.isValid()) {
      return;
    }

    const model = this.formModel();
    if (this.isEditMode()) {
      this.saved.emit({
        displayName: model.displayName,
        applicationType: model.applicationType,
        clientType: model.clientType,
        consentType: model.consentType,
        redirectUris: model.redirectUris,
        postLogoutRedirectUris: model.postLogoutRedirectUris,
        permissions: model.permissions,
        requirements: model.requirements
      });
      return;
    }

    this.saved.emit({
      clientId: model.clientId.trim(),
      displayName: model.displayName,
      applicationType: model.applicationType,
      clientType: model.clientType,
      consentType: model.consentType,
      redirectUris: model.redirectUris,
      postLogoutRedirectUris: model.postLogoutRedirectUris,
      permissions: model.permissions,
      requirements: model.requirements
    });
  }

  private addUri(listKey: 'redirectUris' | 'postLogoutRedirectUris', inputKey: 'redirectUriInput' | 'postLogoutRedirectUriInput') {
    const value = this.formModel()[inputKey].trim();
    if (!value || !this.isValidRedirectUri(value)) {
      return;
    }

    this.formModel.update(model => ({
      ...model,
      [listKey]: model[listKey].includes(value) ? model[listKey] : [...model[listKey], value],
      [inputKey]: ''
    }));
  }

  private isValidRedirectUri(value: string) {
    if (!/^[a-z][a-z0-9+.-]*:/i.test(value) || /\s/.test(value)) {
      return false;
    }

    try {
      const uri = new URL(value);
      return !!uri.protocol && !uri.hash;
    } catch {
      return false;
    }
  }

  private ensurePkce(requirements: string[]) {
    return requirements.includes('ft:pkce') ? requirements : [...requirements, 'ft:pkce'];
  }
}
