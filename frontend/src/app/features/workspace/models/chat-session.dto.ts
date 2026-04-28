import { InlineDataPart } from '../../platform/models/chat-stream-event.dto';
import { ModelCategory } from '../../platform/models/model-option.dto';
import { ModelVendor } from '../../../shared/models/model-vendor.enum';

export type ChatMessageRole = 'user' | 'assistant' | 'system';

export interface ChatMessageOutputDto {
  id: string;
  sessionId: string;
  role: ChatMessageRole;
  content: string;
  attachments?: InlineDataPart[];
  creationTime: string;
  isStreaming?: boolean;
}

export interface GetChatMessagePagedInputDto {
  cursorMessageId?: string;
  limit?: number;
}

export interface ChatSessionOutputDto {
  id: string;
  title: string;
  providerGroupId?: string;
  modelId: string;
  accountId?: string;
  creationTime: string;
  lastMessageTime?: string;
  lastMessagePreview?: string;
  messageCount: number;
}

export interface CreateChatSessionInputDto {
  title?: string;
  providerGroupId?: string;
  modelId: string;
  accountId?: string;
}

export interface UpdateChatSessionInputDto {
  title?: string;
  providerGroupId?: string;
  useAutoProviderGroup?: boolean;
  modelId?: string;
  accountId?: string;
}

export interface SendChatMessageInputDto {
  content: string;
  attachments?: InlineDataPart[];
}

export interface ChatModelOptionOutputDto {
  label: string;
  value: string;
  category?: ModelCategory;
  vendor?: ModelVendor;
  providerGroupId?: string;
  providerGroupName?: string;
}
