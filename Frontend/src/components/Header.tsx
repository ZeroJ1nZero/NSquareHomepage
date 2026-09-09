import React from 'react';
import { Link, NavLink } from 'react-router-dom';
import type { CurrentUser } from '../types';

interface HeaderProps {
  user: CurrentUser;
  onOpenLogin: () => void;
  onLogout: () => void;
}

export const Header: React.FC<HeaderProps> = ({ user, onOpenLogin, onLogout }) => {
  return (
    <header className="site-header">
      <div className="container header-container">
        <div className="brand">
          <Link to="/" className="brand-link">
            <span className="brand-logo">N²</span>
            <div className="brand-text">
              <span className="brand-title">N-SQUARE</span>
              <span className="brand-subtitle">Enterprise Gateway</span>
            </div>
          </Link>
        </div>

        <nav className="nav-links">
          <NavLink
            to="/"
            end
            className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}
          >
            홈
          </NavLink>
          <NavLink
            to="/about"
            className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}
          >
            회사 소개
          </NavLink>
          <NavLink
            to="/service"
            className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}
          >
            주요 서비스
          </NavLink>
          <NavLink
            to="/history"
            className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}
          >
            회사 연혁
          </NavLink>
          <a
            href="https://localhost:7001/swagger"
            target="_blank"
            rel="noreferrer"
            className="nav-link swagger-link"
          >
            Gateway API ↗
          </a>
        </nav>

        <div className="auth-widget">
          {user.isAuthenticated ? (
            <div className="user-badge-wrap">
              <span className="user-role-badge">{user.role}</span>
              <span className="user-name-label">{user.userName}</span>
              <button
                onClick={onLogout}
                className="btn btn-secondary btn-sm"
                style={{ marginLeft: '8px' }}
              >
                로그아웃
              </button>
            </div>
          ) : (
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <button
                onClick={onOpenLogin}
                className="btn btn-primary btn-sm"
                title="ServiceServer Step 1 -> AuthServer OIDC SSO 로그인"
              >
                <span className="icon">🔐</span> SSO 로그인
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );
};
