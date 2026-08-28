import type { AboutData, CurrentUser, HistoryItem, ServiceData } from './types';

/**
 * [Frontend API Client]
 * N-SQUARE 통합 플랫폼 클라이언트 API 모듈
 * 
 * - 트랙 A (공개 조회): /api/public/*
 * - 트랙 B (관리자 CUD): /api/admin/* (BFF 서비스 세션 쿠키 또는 JWT Bearer 연동)
 * - SSO 파이프라인: /api/auth/start-sso (Silent SSO & PKCE)
 */
const API_BASE = '/api';
const AUTH_BASE = '/connect';

const TOKEN_KEY = 'nsq_access_token';
const REFRESH_TOKEN_KEY = 'nsq_refresh_token';
const USER_KEY = 'nsq_user';

/**
 * 로컬 스토리지에 보관된 Access Token을 조회합니다.
 */
export function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

/**
 * 로그인 성공 시 발급받은 토큰 세트 및 사용자 정보를 보관합니다.
 */
export function setStoredTokens(accessToken: string, refreshToken?: string, user?: CurrentUser) {
  localStorage.setItem(TOKEN_KEY, accessToken);
  if (refreshToken) {
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
  }
  if (user) {
    localStorage.setItem(USER_KEY, JSON.stringify(user));
  }
}

/**
 * 로그아웃 시 로컬 스토리지의 토큰 및 세션 정보를 정리합니다.
 */
export function clearStoredTokens() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
}

/**
 * JWT 페이로드를 Base64Url 디코딩하여 클레임 객체를 추출합니다.
 */
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

/**
 * Authorization Bearer 헤더를 생성합니다.
 */
function getAuthHeader(): Record<string, string> {
  const token = getStoredToken();
  if (token) {
    return { Authorization: `Bearer ${token}` };
  }
  return {};
}

/**
 * [Step 1 SSO 시작] OIDC SSO 플로우를 가동하여 인증 서버(IdP)로 이동합니다.
 * @param returnUrl 인증 완료 후 최종 복귀할 프론트엔드 주소 (기본값: 현재 페이지)
 */
export function startSso(returnUrl?: string, service?: string): void {
  const target = returnUrl || (window.location.origin + window.location.pathname);
  const serviceParam = service ? `&service=${encodeURIComponent(service)}` : '';
  
  try {
    sessionStorage.setItem('sso_return_url', target);
    if (service) {
      sessionStorage.setItem('sso_target_service', service);
    } else {
      sessionStorage.removeItem('sso_target_service');
    }
  } catch (e) {
    console.warn('sessionStorage error:', e);
  }

  window.location.href = `${API_BASE}/auth/access-sso?returnUrl=${encodeURIComponent(target)}&autoRedirect=true${serviceParam}`;
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
    const res = await fetch(`${API_BASE}/auth/user-identity`, {
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
          activeSessions: data.activeSessions,
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
    const user = JSON.parse(rawUser) as CurrentUser;
    return user;
  } catch {
    clearStoredTokens();
    throw new Error('인증 세션 만료');
  }
}

/**
 * 전역 SSO 로그아웃 (ServiceServer 세션 및 AuthServer 전역 SSO 세션 파기)
 */
export async function logout(): Promise<void> {
  clearStoredTokens();
  try {
    sessionStorage.clear();
    localStorage.clear();
  } catch (e) {
    console.warn('Storage clear warning:', e);
  }

  try {
    const res = await fetch(`${API_BASE}/auth/logout?returnUrl=${encodeURIComponent('http://localhost:3000/')}`, {
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
  // Fallback 직접 AuthServer 로그아웃으로 이동하여 AuthServer_SSO_Cookie 파기 후 http://localhost:3000/ 으로 복귀
  window.location.href = `https://localhost:7213/api/auth/logout?post_logout_redirect_uri=${encodeURIComponent('http://localhost:3000/')}`;
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
      startSso(window.location.href, 'about');
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
      startSso(window.location.href, 'service');
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
      startSso(window.location.href, 'history');
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
