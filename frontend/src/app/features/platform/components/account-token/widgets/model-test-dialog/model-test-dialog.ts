import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { ImageModule } from 'primeng/image';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { finalize, Subscription } from 'rxjs';

import { PlatformIcon } from '../../../../../../shared/components/platform-icon/platform-icon';
import { DIALOG_CONFIGS } from '../../../../../../shared/constants/dialog-config.constants';
import { Provider } from '../../../../../../shared/models/provider.enum';
import { AccountTokenOutputDto } from '../../../../models/account-token.dto';
import { ChatMessageInputDto } from '../../../../models/chat-message-input.dto';
import { ModelOptionOutputDto } from '../../../../models/model-option.dto';
import { AccountTokenService } from '../../../../services/account-token-service';

export type ContentBlock = { type: 'text'; text: string } | { type: 'image'; mimeType: string; data?: string; url?: string };

@Component({
  selector: 'app-model-test-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    ButtonModule,
    SelectModule,
    MessageModule,
    ProgressSpinnerModule,
    TextareaModule,
    ImageModule,
    PlatformIcon
  ],
  templateUrl: './model-test-dialog.html',
  styles: [
    `
      .terminal-output {
        font-family: 'JetBrains Mono', 'Consolas', 'Monaco', monospace;
        white-space: pre-wrap;
      }
      .cursor::after {
        content: '▋';
        animation: blink 1s step-start infinite;
      }
      @keyframes blink {
        0%,
        100% {
          opacity: 1;
        }
        50% {
          opacity: 0;
        }
      }
    `
  ]
})
export class ModelTestDialog {
  private service = inject(AccountTokenService);

  visible = signal(false);
  loading = signal(false);
  testing = signal(false);
  account = signal<AccountTokenOutputDto | null>(null);

  // 使用中型 Dialog 配置
  dialogConfig = DIALOG_CONFIGS.MEDIUM;

  private testSub?: Subscription;

  // Test State
  selectedModel = signal<string | null>(null);
  systemPrompt = signal('你好，当前使用的是什么模型？');
  errorMessage = signal('');
  systemMessages = signal<string[]>([]);
  contentBlocks = signal<ContentBlock[]>([]);

  // Model loading error (separate from test error)
  modelLoadError = signal('');

  // Model Options (dynamically loaded from backend)
  modelOptions = signal<ModelOptionOutputDto[]>([]);

  open(account: AccountTokenOutputDto) {
    this.account.set(account);
    this.contentBlocks.set([]);
    this.errorMessage.set('');
    this.modelLoadError.set('');
    this.systemMessages.set([]);
    this.testing.set(false);
    this.selectedModel.set(null); // 清空选中的模型
    this.modelOptions.set([]); // 🆕 清空模型列表
    this.systemPrompt.set('你好，当前使用的是什么模型？');
    this.visible.set(true);

    // Load models from backend API (with accountId for upstream fetch)
    this.loading.set(true);
    this.service
      .getAvailableModels(account.provider, account.id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: models => {
          this.modelOptions.set(models);
          // Auto-select first model
          if (models.length > 0) {
            this.selectedModel.set(models[0].value);
          }
        },
        error: (err: any) => {
          console.error('Failed to load models', err);
          this.modelLoadError.set('加载模型列表失败，请重试。');
        }
      });
  }

  startTest() {
    if (this.testing() || !this.account()) return;

    this.testing.set(true);
    this.contentBlocks.set([]);
    this.errorMessage.set('');
    this.modelLoadError.set('');
    this.systemMessages.set([]);

    const input: ChatMessageInputDto = {
      modelId: this.selectedModel() ?? '',
      message: this.systemPrompt()
    };

    this.testSub?.unsubscribe();
    this.testSub = this.service
      .debugModel(this.account()!.id, input)
      .pipe(
        finalize(() => {
          this.testing.set(false);
        })
      )
      .subscribe({
        next: event => {
          switch (event.type) {
            case 'System':
              this.systemMessages.update(msgs => [...msgs, event.content!]);
              break;
            case 'Error':
              this.errorMessage.set(event.content ?? '未知错误');
              break;
            case 'Content':
              if (event.content) {
                this.contentBlocks.update(blocks => {
                  const last = blocks[blocks.length - 1];
                  if (last?.type === 'text') {
                    return [...blocks.slice(0, -1), { type: 'text' as const, text: last.text + event.content! }];
                  }
                  return [...blocks, { type: 'text' as const, text: event.content! }];
                });
              }

              if (event.inlineData && event.inlineData.length > 0) {
                this.contentBlocks.update(blocks => [
                  ...blocks,
                  ...event.inlineData!.map(part => ({
                    type: 'image' as const,
                    mimeType: part.mimeType,
                    data: part.data,
                    url: part.url
                  }))
                ]);
              }
              break;
          }
        },
        error: (err: any) => {
          this.errorMessage.set(err.message || '发生未知错误');
        }
      });
  }

  onPromptKeyDown(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.startTest();
    }
  }

  close() {
    this.testSub?.unsubscribe();
    this.visible.set(false);
  }
}
