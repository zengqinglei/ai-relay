import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, SecurityContext, computed, inject, input } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { ImageModule } from 'primeng/image';
import MarkdownIt from 'markdown-it';

import { ChatMessageOutputDto } from '../../../../models/chat-session.dto';

@Component({
  selector: 'app-message-bubble',
  standalone: true,
  imports: [CommonModule, ImageModule],
  templateUrl: './message-bubble.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [
    `
      .message-markdown {
        color: inherit;
        line-height: 1.7;
        word-break: break-word;
      }
      .message-markdown > :first-child { margin-top: 0; }
      .message-markdown > :last-child { margin-bottom: 0; }
      .message-markdown p,
      .message-markdown ul,
      .message-markdown ol,
      .message-markdown pre,
      .message-markdown blockquote,
      .message-markdown h1,
      .message-markdown h2,
      .message-markdown h3 { margin: 0 0 0.75rem; }
      .message-markdown ul,
      .message-markdown ol { padding-left: 1.25rem; }
      .message-markdown pre {
        overflow: auto;
        border-radius: 1rem;
        background: color-mix(in srgb, var(--p-surface-900) 92%, transparent);
        padding: 0.875rem 1rem;
        color: var(--p-surface-0);
      }
      .message-markdown code {
        font-family: 'JetBrains Mono', 'Consolas', monospace;
        font-size: 0.9em;
      }
      .message-markdown :not(pre) > code {
        border-radius: 0.5rem;
        background: color-mix(in srgb, var(--p-surface-400) 12%, transparent);
        padding: 0.15rem 0.35rem;
      }
      .stream-cursor::after {
        content: '▋';
        animation: blink 1s step-start infinite;
      }
      .stream-dots span {
        height: 0.26rem;
        width: 0.26rem;
        border-radius: 9999px;
        background: currentColor;
        opacity: 0.35;
        animation: dotPulse 1.2s ease-in-out infinite;
      }
      .stream-dots span:nth-child(2) { animation-delay: 0.15s; }
      .stream-dots span:nth-child(3) { animation-delay: 0.3s; }
      @keyframes blink {
        0%, 100% { opacity: 1; }
        50% { opacity: 0; }
      }
      @keyframes dotPulse {
        0%, 80%, 100% { transform: translateY(0); opacity: 0.3; }
        40% { transform: translateY(-0.08rem); opacity: 1; }
      }
    `
  ]
})
export class MessageBubble {
  message = input.required<ChatMessageOutputDto>();

  private readonly sanitizer = inject(DomSanitizer);
  private readonly markdown = new MarkdownIt({ html: false, linkify: true, typographer: true, breaks: true });

  readonly isUser = computed(() => this.message().role === 'user');
  readonly avatarLabel = computed(() => (this.isUser() ? '我' : 'AI'));
  readonly showStreamingIndicator = computed(() => !this.isUser() && !!this.message().isStreaming);
  readonly renderedHtml = computed(() => {
    const raw = this.markdown.render(this.message().content || '');
    return this.sanitizer.sanitize(SecurityContext.HTML, raw) ?? '';
  });

  getImageSrc(attachment: { mimeType: string; data?: string; url?: string }) {
    if (attachment.url) {
      return attachment.url;
    }

    return attachment.data ? `data:${attachment.mimeType};base64,${attachment.data}` : '';
  }

  isImageAttachment(attachment: { mimeType: string }) {
    return attachment.mimeType.startsWith('image/');
  }
}

