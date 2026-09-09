import React from 'react';
import type { CurrentUser } from '../types';

interface HeroSectionProps {
  user: CurrentUser;
}

export const HeroSection: React.FC<HeroSectionProps> = ({ user }) => {
  return (
    <section className="hero-section">
      <div className="container">
        <div className="hero-badge">Microservices &amp; Clean Architecture Ecosystem</div>
        <h1 className="hero-title">엔스퀘어 사내 통합 시스템</h1>
        <p className="hero-desc">
          독립된 <strong>React 프론트엔드</strong>와 <strong>API 게이트웨이 / 리소스 / 인증 서버</strong>로 분리된 마이크로서비스 플랫폼입니다.<br />
          공개 정보는 <strong>트랙 A(비로그인 즉시 조회)</strong>로, 관리자 작업은 <strong>트랙 B(Role == Admin 검증 및 JWT 발급)</strong>로 안전하게 처리됩니다.
        </p>

        <div className="system-status-grid">
          <div className="status-card">
            <div className="status-header">
              <span className="status-indicator active"></span>
              <span className="status-label">🌐 Frontend SPA</span>
            </div>
            <span className="status-val">:3000 (React + Vite)</span>
          </div>

          <div className="status-card">
            <div className="status-header">
              <span className="status-indicator active"></span>
              <span className="status-label">🖥️ ServiceServer</span>
            </div>
            <span className="status-val">:7001 (API Gateway)</span>
          </div>

          <div className="status-card">
            <div className="status-header">
              <span className="status-indicator active"></span>
              <span className="status-label">📦 ResourceServer</span>
            </div>
            <span className="status-val">:7002 (CRUD API)</span>
          </div>

          <div className="status-card">
            <div className="status-header">
              <span className="status-indicator active"></span>
              <span className="status-label">🔐 AuthServer</span>
            </div>
            <span className="status-val">:7213 (IdP SSO)</span>
          </div>

          <div className="status-card highlight">
            <div className="status-header">
              <span className={`status-indicator ${user.isAuthenticated ? 'admin' : ''}`}></span>
              <span className="status-label">🛡️ 현재 권한 상태</span>
            </div>
            <span className="status-val">
              {user.isAuthenticated ? `관리자 (${user.role}) [트랙 B]` : '비로그인 [트랙 A]'}
            </span>
          </div>
        </div>
      </div>
    </section>
  );
};
