import type { AboutData, CurrentUser, HistoryItem, ServiceData } from './types';

/**
 * [Frontend API Client]
 * N-SQUARE 통합 플랫폼 클라이언트 API 모듈 (Zero-Token BFF Pattern)
 * 
 * - 프론트엔드(React)는 어떠한 JWT 토큰(Access / ID / Refresh)도 소유하거나 브라우저에 저장하지 않습니다.
 * - 모든 사용자 인증 및 관리는 HttpOnly 보안 세션 쿠키(credentials: 'include')를 통해 처리됩니다.
 * - 트랙 A (공개 조회): /api/public/*
 * - 트랙 B (관리자 CUD): /api/admin/* (BFF 서비스 세션 쿠키 연동)
 * - SSO 파이프라인: /api/auth/access-sso (Silent SSO & PKCE)
 */
const API_BASE = '/api';

/**
 * [Step 1 SSO 시작] OIDC SSO 플로우를 가동하여 ServiceServer 게이트웨이를 통해 IdP로 이동합니다.
 * @param returnUrl 인증 완료 후 최종 복귀할 프론트엔드 주소 (기본값: 현재 페이지)
 * @param service 특정 서비스 영역 ('about' | 'service' | 'history' | 'none')
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
 * 현재 로그인 상태 확인 (ServiceServer HttpOnly 세션 쿠키 기반)
 */
export async function getCurrentUser(): Promise<CurrentUser> {
  const res = await fetch(`${API_BASE}/auth/user-identity`, {
    credentials: 'include',
  });

  if (!res.ok) {
    throw new Error('인증 상태 조회 실패');
  }

  const data = await res.json();
  if (!data.isAuthenticated) {
    throw new Error('비로그인 상태');
  }

  return {
    isAuthenticated: true,
    userName: data.userName || '관리자',
    role: data.role || 'Admin',
    email: data.email,
    activeSessions: data.activeSessions,
  };
}

/**
 * 전역 SSO 로그아웃 (ServiceServer 세션 및 AuthServer 전역 SSO 세션 파기)
 */
export async function logout(): Promise<void> {
  try {
    sessionStorage.clear();
    localStorage.clear();
  } catch (e) {
    console.warn('Storage clear warning:', e);
  }

  // 1. ServiceServer 서비스 세션 쿠키 파기
  try {
    await fetch(`${API_BASE}/auth/logout`, {
      method: 'POST',
      credentials: 'include',
    });
  } catch (err) {
    console.warn('ServiceServer logout error:', err);
  }

  // 2. AuthServer 전역 SSO 세션 쿠키 파기 (Vite 프록시 /connect/logout 경유)
  try {
    await fetch('/connect/logout', {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      credentials: 'include',
    });
  } catch (err) {
    console.warn('AuthServer logout error:', err);
  }

  // 3. 브라우저 외부 리다이렉트 없이 깔끔하게 메인 페이지로 복귀
  window.location.href = '/';
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
