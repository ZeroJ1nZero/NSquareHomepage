export interface CurrentUser {
  isAuthenticated: boolean;
  userName: string | null;
  displayName?: string | null;
  role: string | null;
  email?: string | null;
  activeSessions?: {
    about?: boolean;
    service?: boolean;
    history?: boolean;
  };
}

export interface AboutData {
  content?: string;
  introduction?: string;
  updatedAt?: string | null;
}

export interface ServiceData {
  content?: string;
  service?: string;
  updatedAt?: string | null;
}

export interface HistoryItem {
  id?: number;
  eventDate?: string;
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
