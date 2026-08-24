export interface CurrentUser {
  isAuthenticated: boolean;
  userName: string | null;
  role: string | null;
  email?: string | null;
}

export interface AboutData {
  content?: string;
  introduction?: string;
  updatedAt?: string | null;
}

export interface ServiceData {
  service?: string;
  updatedAt?: string | null;
}

export interface HistoryItem {
  id?: number;
  data?: string;
  date?: string;
  content: string;
}

export interface LogEntry {
  id: string;
  time: string;
  tag: string;
  msg: string;
  type: 'info' | 'success' | 'warn' | 'error';
}

export interface ToastMessage {
  id: string;
  message: string;
  type: 'info' | 'success' | 'error';
}
