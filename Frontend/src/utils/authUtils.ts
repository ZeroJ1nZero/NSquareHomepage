import type { CurrentUser } from '../types';
import * as api from '../api';

export function checkServicePermission(
  user: CurrentUser,
  serviceKey: 'about' | 'service' | 'history',
  addLog?: (tag: string, msg: string, type?: 'info' | 'success' | 'warn' | 'error') => void,
  actionName: string = '이 작업'
): boolean {
  if (!user.activeSessions?.[serviceKey]) {
    addLog?.(
      '권한 검사',
      `[302 리다이렉트] ${actionName} 수행 전 ${serviceKey} 서비스 세션 쿠키 부재 감지 ➔ SSO 쿠키 확인 및 세션 발급 파이프라인 가동`,
      'warn'
    );
    api.startSso(window.location.href, serviceKey);
    return false;
  }

  addLog?.(
    '권한 검사',
    `[승인] 사용자 ${user.userName || '관리자'}(Admin)의 ${serviceKey} 서비스 세션 쿠키 검증 완료!`,
    'success'
  );
  return true;
}

export function checkAdminPermission(
  user: CurrentUser,
  onOpenLogin: () => void,
  showToast: (msg: string, type?: 'info' | 'success' | 'error') => void,
  addLog?: (tag: string, msg: string, type?: 'info' | 'success' | 'warn' | 'error') => void,
  actionName: string = '이 작업'
): boolean {
  if (!user.isAuthenticated) {
    addLog?.('권한 검사', `[차단] ${actionName} 수행 전 비로그인 상태 감지 ➔ SSO 로그인 화면으로 이동합니다.`, 'warn');
    showToast(`${actionName}을(를) 수행하려면 관리자 로그인이 필요합니다.`, 'error');
    onOpenLogin();
    return false;
  }

  if (user.role !== 'Admin') {
    addLog?.('권한 검사', `[차단] 현재 계정(${user.userName}, Role: ${user.role})은 관리자(Admin) 권한이 없습니다.`, 'error');
    showToast(`관리자(Admin) 권한이 필요합니다. (현재: ${user.role || '일반 유저'})`, 'error');
    return false;
  }

  addLog?.('권한 검사', `[승인] 사용자 ${user.userName}(Admin)의 ${actionName} 권한이 확인되었습니다.`, 'success');
  return true;
}
