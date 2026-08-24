import type { AboutData, CurrentUser, HistoryItem, ServiceData } from './types';

// API base path (uses Vite proxy or direct relative path)
const API_BASE = '/api';
const AUTH_BASE = '/connect';

const TOKEN_KEY = 'nsq_access_token';
const REFRESH_TOKEN_KEY = 'nsq_refresh_token';
const USER_KEY = 'nsq_user';

export function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setStoredTokens(accessToken: string, refreshToken?: string, user?: CurrentUser) {
  localStorage.setItem(TOKEN_KEY, accessToken);
  if (refreshToken) {
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
  }
  if (user) {
    localStorage.setItem(USER_KEY, JSON.stringify(user));
  }
}

export function clearStoredTokens() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
}

function parseJwt(token: string): any {
  try {
    const base64Url = token.split('.')[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    return JSON.parse(jsonPayload);
  } catch {
    return {};
  }
}

function getAuthHeader(): Record<string, string> {
  const token = getStoredToken();
  if (token) {
    return { Authorization: `Bearer ${token}` };
  }
  return {};
}

/**
 * 서비스 서버 Step 1 OIDC SSO 시작 (PKCE & CSRF 자동 발급 후 AuthServer 로그인으로 이동)
 */
export function startSso(returnUrl?: string): void {
  const target = returnUrl || (window.location.origin + window.location.pathname);
  window.location.href = `${API_BASE}/auth/start-sso?returnUrl=${encodeURIComponent(target)}&autoRedirect=true`;
}

/**
 * 클라이언트 ➔ 인증 서버 직접 로그인 (OIDC Password Grant Flow)
 */
export async function login(email: string, password: string): Promise<{ success: boolean; message: string; user: CurrentUser }> {
  const params = new URLSearchParams();
  params.append('grant_type', 'password');
  params.append('client_id', 'company-homepage');
  params.append('username', email);
  params.append('password', password);
  params.append('scope', 'openid profile email roles offline_access');

  const res = await fetch(`${AUTH_BASE}/token`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded',
    },
    body: params.toString(),
  });

  const data = await res.json();
  if (!res.ok) {
    throw new Error(data.error_description || data.error || '로그인에 실패했습니다. 아이디 및 비밀번호를 확인해 주세요.');
  }

  const accessToken = data.access_token;
  let userName = email;
  let role = 'User';

  if (data.id_token) {
    const decoded = parseJwt(data.id_token);
    userName = decoded.name || decoded.preferred_username || email;
    role = decoded.role || 'User';
  } else if (accessToken) {
    const decoded = parseJwt(accessToken);
    userName = decoded.name || email;
    role = decoded.role || 'User';
  }

  const currentUser: CurrentUser = {
    isAuthenticated: true,
    userName,
    role,
    email,
  };

  setStoredTokens(accessToken, data.refresh_token, currentUser);

  return {
    success: true,
    message: '인증 서버로부터 JWT 토큰 발급 및 로그인이 완료되었습니다.',
    user: currentUser,
  };
}

/**
 * 현재 로그인 상태 확인 (1순위: ServiceServer 세션 쿠키, 2순위: 로컬 스토리지 JWT)
 */
export async function getCurrentUser(): Promise<CurrentUser> {
  // 1. ServiceServer 세션 쿠키 (.NsqHomepage.ServiceSession) 확인
  try {
    const res = await fetch(`${API_BASE}/auth/me`, {
      credentials: 'include',
      headers: {
        ...getAuthHeader(),
      },
    });

    if (res.ok) {
      const data = await res.json();
      if (data.isAuthenticated) {
        const user: CurrentUser = {
          isAuthenticated: true,
          userName: data.userName || '관리자',
          role: data.role || 'Admin',
          email: data.email,
        };
        localStorage.setItem(USER_KEY, JSON.stringify(user));
        return user;
      }
    }
  } catch {
    // 세션 쿠키 확인 실패 시 로컬 스토리지 확인으로 넘어감
  }

  // 2. 로컬 스토리지 JWT 토큰 확인
  const token = getStoredToken();
  const rawUser = localStorage.getItem(USER_KEY);
  if (!token || !rawUser) {
    throw new Error('비로그인 상태');
  }

  try {
    const user: CurrentUser = JSON.parse(rawUser);
    const decoded = parseJwt(token);
    if (decoded.exp && decoded.exp * 1000 < Date.now()) {
      clearStoredTokens();
      throw new Error('토큰 만료');
    }
    return user;
  } catch {
    clearStoredTokens();
    throw new Error('유효하지 않은 세션');
  }
}

export async function logout(): Promise<void> {
  clearStoredTokens();
  try {
    const res = await fetch(`${API_BASE}/auth/logout`, {
      method: 'POST',
      credentials: 'include',
    });
    if (res.ok) {
      const data = await res.json().catch(() => null);
      if (data?.logoutUrl) {
        window.location.href = data.logoutUrl;
        return;
      }
    }
  } catch (err) {
    console.warn('Logout API error:', err);
  }
  // Fallback 직접 AuthServer 로그아웃으로 이동하여 AuthServer_SSO_Cookie 파기
  window.location.href = `https://localhost:7213/connect/logout?post_logout_redirect_uri=${encodeURIComponent(window.location.origin)}`;
}

export async function getCompanyAbout(): Promise<AboutData> {
  const res = await fetch(`${API_BASE}/public/company-about`, {
    credentials: 'include',
  });
  if (!res.ok) throw new Error(`회사 소개 조회 실패 (${res.status})`);
  return await res.json();
}

export async function updateCompanyAbout(content: string): Promise<AboutData> {
  const res = await fetch(`${API_BASE}/admin/company-about`, {
    method: 'PUT',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...getAuthHeader(),
    },
    body: JSON.stringify({ content }),
  });
  if (res.status === 302 || res.status === 401) {
    const data = await res.json().catch(() => null);
    if (data?.authorize_url) {
      window.location.href = data.authorize_url;
    } else {
      startSso(window.location.href);
    }
    throw new Error('서비스 세션 쿠키 발급 파이프라인으로 이동합니다.');
  }
  if (!res.ok) throw new Error(`회사 소개 수정 실패 (${res.status})`);
  return await res.json();
}

export async function getCompanyServices(): Promise<ServiceData> {
  const res = await fetch(`${API_BASE}/public/company-services`, {
    credentials: 'include',
  });
  if (!res.ok) throw new Error(`회사 서비스 조회 실패 (${res.status})`);
  return await res.json();
}

export async function updateCompanyServices(service: string): Promise<ServiceData> {
  const res = await fetch(`${API_BASE}/admin/company-services`, {
    method: 'PUT',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...getAuthHeader(),
    },
    body: JSON.stringify({ service }),
  });
  if (res.status === 302 || res.status === 401) {
    const data = await res.json().catch(() => null);
    if (data?.authorize_url) {
      window.location.href = data.authorize_url;
    } else {
      startSso(window.location.href);
    }
    throw new Error('서비스 세션 쿠키 발급 파이프라인으로 이동합니다.');
  }
  if (!res.ok) throw new Error(`회사 서비스 수정 실패 (${res.status})`);
  return await res.json();
}

export async function getCompanyHistories(): Promise<HistoryItem[]> {
  const res = await fetch(`${API_BASE}/public/company-histories`, {
    credentials: 'include',
  });
  if (!res.ok) throw new Error(`회사 연혁 조회 실패 (${res.status})`);
  const data = await res.json();
  return data.history || [];
}

export async function addCompanyHistory(date: string, content: string): Promise<HistoryItem[]> {
  const res = await fetch(`${API_BASE}/admin/company-histories`, {
    method: 'PUT',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...getAuthHeader(),
    },
    body: JSON.stringify({
      history: [{ data: date, content }],
    }),
  });
  if (res.status === 302 || res.status === 401) {
    const data = await res.json().catch(() => null);
    if (data?.authorize_url) {
      window.location.href = data.authorize_url;
    } else {
      startSso(window.location.href);
    }
    throw new Error('서비스 세션 쿠키 발급 파이프라인으로 이동합니다.');
  }
  if (!res.ok) throw new Error(`연혁 추가 실패 (${res.status})`);
  const data = await res.json();
  return data.history || [];
}

export async function deleteCompanyHistory(id: number): Promise<void> {
  const res = await fetch(`${API_BASE}/admin/company-histories/${id}`, {
    method: 'DELETE',
    credentials: 'include',
    headers: {
      ...getAuthHeader(),
    },
  });
  if (res.status === 302 || res.status === 401) {
    const data = await res.json().catch(() => null);
    if (data?.authorize_url) {
      window.location.href = data.authorize_url;
    } else {
      startSso(window.location.href);
    }
    throw new Error('서비스 세션 쿠키 발급 파이프라인으로 이동합니다.');
  }
  if (!res.ok) throw new Error(`연혁 삭제 실패 (${res.status})`);
}
