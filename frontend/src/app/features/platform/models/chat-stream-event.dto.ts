export interface ChatStreamEvent {
  content?: string;
  error?: string;
  systemMessage?: string;
  isComplete?: boolean;
  inlineData?: InlineDataPart;
}

export interface InlineDataPart {
  mimeType: string;
  data: string;
}
