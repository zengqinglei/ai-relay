import { InlineDataPart } from '../../platform/models/chat-stream-event.dto';

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

export interface ChatSessionOutputDto {
  id: string;
  title: string;
  providerGroupId: string;
  modelId: string;
  accountId?: string;
  messages: ChatMessageOutputDto[];
  creationTime: string;
  lastMessageTime?: string;
}

export interface CreateChatSessionInputDto {
  title?: string;
  providerGroupId: string;
  modelId: string;
  accountId?: string;
}

export interface UpdateChatSessionInputDto {
  title?: string;
  providerGroupId?: string;
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
}
