export type ChatStreamEventType = 'Content' | 'Error' | 'System';

export interface ChatStreamEvent {
  type?: ChatStreamEventType;
  content?: string;
  reasoningContent?: string;
  isComplete?: boolean;
  inlineData?: InlineDataPart[];
}

export interface InlineDataPart {
  mimeType: string;
  data?: string;
  url?: string;
}
