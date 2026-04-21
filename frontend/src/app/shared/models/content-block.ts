export type ContentBlock =
  | { type: 'text'; text: string }
  | { type: 'image'; mimeType: string; data?: string; url?: string };
