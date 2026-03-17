export interface ChatStreamEvent {
  content?: string;
  error?: string;
  systemMessage?: string;
  isComplete?: boolean;
}
