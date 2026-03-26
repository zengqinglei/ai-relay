import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class FilterStateService {
  load<T>(key: string): Partial<T> {
    try {
      const raw = localStorage.getItem(`filter:${key}`);
      return raw ? JSON.parse(raw) : {};
    } catch {
      return {};
    }
  }

  save<T extends object>(key: string, state: T): void {
    try {
      localStorage.setItem(`filter:${key}`, JSON.stringify(state));
    } catch {
      // localStorage 不可用时静默忽略
    }
  }

  clear(key: string): void {
    localStorage.removeItem(`filter:${key}`);
  }
}
