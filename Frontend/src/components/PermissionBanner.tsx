import React from 'react';
import type { CurrentUser } from '../types';

interface PermissionBannerProps {
  user: CurrentUser;
  pageName: string;
  onOpenLogin: () => void;
}

export const PermissionBanner: React.FC<PermissionBannerProps> = ({ user, pageName, onOpenLogin }) => {
  const isAdmin = user.isAuthenticated && user.role === 'Admin';

  return (
    <div className={`permission-banner ${isAdmin ? 'admin-granted' : 'guest-restricted'}`}>
      <div className="banner-left">
        <span className="banner-icon">{isAdmin ? '🛡️' : '🔒'}</span>
        <div className="banner-text">
          <div className="banner-title">
            <strong>{pageName}</strong> {isAdmin ? '관리자 권한 활성화됨' : '권한 제한 (읽기 전용)'}
          </div>
          <div className="banner-desc">
            {isAdmin
              ? `현재 ${user.userName || '관리자'}(Role: Admin) 계정으로 인증되어 콘텐츠 수정 및 삭제가 가능합니다.`
              : '수정 또는 삭제 작업을 수행하려면 관리자(Admin) 권한으로 로그인이 필요합니다.'}
          </div>
        </div>
      </div>
      <div className="banner-right">
        {!isAdmin && (
          <button onClick={onOpenLogin} className="btn btn-primary btn-xs">
            🔐 SSO 로그인
          </button>
        )}
        {isAdmin && (
          <span className="badge badge-success">
            ✓ 수정 권한 보유
          </span>
        )}
      </div>
    </div>
  );
};
