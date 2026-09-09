import React from 'react';
import type { CurrentUser } from '../types';

interface PermissionBannerProps {
  user: CurrentUser;
  pageName: string;
  serviceKey: 'about' | 'service' | 'history';
  onOpenLogin?: () => void;
}

export const PermissionBanner: React.FC<PermissionBannerProps> = ({ user, pageName, serviceKey }) => {
  const hasServiceSession = Boolean(user.activeSessions?.[serviceKey]);

  return (
    <div className={`permission-banner ${hasServiceSession ? 'admin-granted' : 'guest-restricted'}`}>
      <div className="banner-left">
        <span className="banner-icon">{hasServiceSession ? '🛡️' : '🔒'}</span>
        <div className="banner-text">
          <div className="banner-title">
            <strong>{pageName}</strong> {hasServiceSession ? '관리자 서비스 세션 보유' : '서비스 세션 미보유'}
          </div>
          <div className="banner-desc">
            {hasServiceSession
              ? `현재 ${user.userName || '관리자'}(Role: Admin) 계정의 ${pageName} 세션 쿠키(.Nsq.${serviceKey.charAt(0).toUpperCase() + serviceKey.slice(1)}.Session)가 활성화되어 있습니다.`
              : `수정/삭제 시 302 리다이렉트를 통해 SSO 쿠키를 확인하고 ${pageName} 세션 쿠키를 발급받습니다.`}
          </div>
        </div>
      </div>
      <div className="banner-right">
        {hasServiceSession ? (
          <span className="badge badge-success">
            ✓ {pageName} 세션 쿠키 활성
          </span>
        ) : (
          <span className="badge badge-warning" style={{ background: '#fef3c7', color: '#92400e', border: '1px solid #fde68a' }}>
            🔒 세션 미보유
          </span>
        )}
      </div>
    </div>
  );
};
