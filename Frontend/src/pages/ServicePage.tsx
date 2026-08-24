import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import type { CurrentUser, LogEntry, ServiceData } from '../types';
import { PermissionBanner } from '../components/PermissionBanner';
import { Inspector } from '../components/Inspector';
import { checkAdminPermission } from '../utils/authUtils';

interface ServicePageProps {
  user: CurrentUser;
  service: ServiceData;
  logs: LogEntry[];
  onSaveService: (service: string) => Promise<void>;
  onOpenLogin: () => void;
  onClearLogs: () => void;
  onRefreshAll: () => Promise<void>;
  showToast: (msg: string, type?: 'info' | 'success' | 'error') => void;
  addLog: (tag: string, msg: string, type?: 'info' | 'success' | 'warn' | 'error') => void;
  formatDate: (date?: string | null) => string;
}

export const ServicePage: React.FC<ServicePageProps> = ({
  user,
  service,
  logs,
  onSaveService,
  onOpenLogin,
  onClearLogs,
  onRefreshAll,
  showToast,
  addLog,
  formatDate,
}) => {
  const [isEditing, setIsEditing] = useState(false);
  const [editContent, setEditContent] = useState('');
  const [isSaving, setIsSaving] = useState(false);

  const handleStartEdit = () => {
    // 🛡️ 권한 검사 수행
    const hasPermission = checkAdminPermission(
      user,
      onOpenLogin,
      showToast,
      addLog,
      '주요 서비스 수정'
    );

    if (!hasPermission) {
      return;
    }

    setEditContent(service.service || '');
    setIsEditing(true);
  };

  const handleCancel = () => {
    setIsEditing(false);
  };

  const handleSave = async () => {
    // 🛡️ 저장 전 권한 재검증
    const hasPermission = checkAdminPermission(
      user,
      onOpenLogin,
      showToast,
      addLog,
      '서비스 저장'
    );

    if (!hasPermission) {
      return;
    }

    if (!editContent.trim()) {
      showToast('서비스 내용을 입력해 주세요.', 'error');
      return;
    }

    setIsSaving(true);
    try {
      await onSaveService(editContent.trim());
      setIsEditing(false);
    } finally {
      setIsSaving(false);
    }
  };

  const displayText = service.service || '(등록된 주요 서비스가 없습니다. 관리자로 로그인하여 등록하세요)';

  return (
    <main className="main-content">
      <div className="container">
        {/* Breadcrumb & Title */}
        <div className="page-header">
          <div className="breadcrumb">
            <Link to="/">홈</Link> <span className="separator">/</span> <span className="current">주요 서비스</span>
          </div>
          <div className="page-title-wrap">
            <h1 className="page-title">⚡ 주요 서비스 (Core Services)</h1>
            <p className="page-subtitle">N-SQUARE 통합 플랫폼이 제공하는 핵심 엔터프라이즈 기능입니다.</p>
          </div>
        </div>

        {/* Permission Check Banner */}
        <PermissionBanner user={user} pageName="주요 서비스 관리" onOpenLogin={onOpenLogin} />

        <div className="content-grid" style={{ marginTop: '24px' }}>
          <div className="data-column">
            <div className="content-card featured-card">
              <div className="card-header">
                <div className="header-left">
                  <span className="section-icon">⚡</span>
                  <h2 className="card-title">현재 운영 중인 주요 서비스</h2>
                </div>
                <div className="header-right">
                  <span className="track-badge track-a">트랙 A: 공개 조회</span>
                  {!isEditing && (
                    <button onClick={handleStartEdit} className="btn btn-primary btn-sm">
                      ✏️ 서비스 수정하기 (권한 검사)
                    </button>
                  )}
                </div>
              </div>

              <div className="card-body">
                {!isEditing ? (
                  <div className="data-view-box">
                    <div className="service-hero-badge" style={{ display: 'inline-block', background: '#eff6ff', color: '#2563eb', padding: '6px 14px', borderRadius: '20px', fontWeight: '700', fontSize: '13px', marginBottom: '14px' }}>
                      🌟 Core Enterprise Feature
                    </div>
                    <p className="data-text" style={{ fontSize: '18px', fontWeight: '600', color: '#1e293b' }}>{displayText}</p>

                    <div className="service-details-grid" style={{ marginTop: '28px', display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px' }}>
                      <div className="service-card-item" style={{ background: '#f8fafc', padding: '18px', borderRadius: '10px', border: '1px solid #e2e8f0' }}>
                        <div style={{ fontSize: '24px', marginBottom: '8px' }}>🔐 OIDC SSO 연동</div>
                        <h4 style={{ fontSize: '15px', fontWeight: '700', marginBottom: '6px' }}>중앙 인증 시스템</h4>
                        <p style={{ fontSize: '13px', color: '#64748b', margin: 0 }}>OpenID Connect 표준 프로토콜 기반 단일 로그인 지원</p>
                      </div>

                      <div className="service-card-item" style={{ background: '#f8fafc', padding: '18px', borderRadius: '10px', border: '1px solid #e2e8f0' }}>
                        <div style={{ fontSize: '24px', marginBottom: '8px' }}>🛡️ Zero-Trust 인가</div>
                        <h4 style={{ fontSize: '15px', fontWeight: '700', marginBottom: '6px' }}>이중 보안 검증</h4>
                        <p style={{ fontSize: '13px', color: '#64748b', margin: 0 }}>JWT Access Token 서명 및 Role 검증 기반 자원 보호</p>
                      </div>

                      <div className="service-card-item" style={{ background: '#f8fafc', padding: '18px', borderRadius: '10px', border: '1px solid #e2e8f0' }}>
                        <div style={{ fontSize: '24px', marginBottom: '8px' }}>🚀 고성능 마이크로서비스</div>
                        <h4 style={{ fontSize: '15px', fontWeight: '700', marginBottom: '6px' }}>확장 가능한 아키텍처</h4>
                        <p style={{ fontSize: '13px', color: '#64748b', margin: 0 }}>독립적인 배포 및 관리가 가능한 4계층 Clean Architecture</p>
                      </div>
                    </div>

                    <div className="data-meta" style={{ marginTop: '24px' }}>
                      <span className="meta-label">최종 갱신 일시:</span>
                      <span className="meta-val">{formatDate(service.updatedAt)}</span>
                    </div>
                  </div>
                ) : (
                  <div className="data-edit-box">
                    <div className="edit-box-header" style={{ marginBottom: '12px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                      <label htmlFor="inputServicePage" className="form-label" style={{ margin: 0 }}>
                        서비스 명세 및 안내 문구 수정 (관리자 권한 확인됨)
                      </label>
                      <span className="badge badge-success">Admin Verified</span>
                    </div>
                    <input
                      type="text"
                      id="inputServicePage"
                      className="form-input"
                      value={editContent}
                      onChange={(e) => setEditContent(e.target.value)}
                      placeholder="주요 서비스 내용을 입력하세요..."
                    />
                    <div className="form-actions">
                      <button onClick={handleCancel} className="btn btn-secondary btn-sm" disabled={isSaving}>
                        취소
                      </button>
                      <button onClick={handleSave} className="btn btn-primary btn-sm" disabled={isSaving}>
                        <span className="icon">💾</span> {isSaving ? '저장 중...' : '서비스 저장 (PUT)'}
                      </button>
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>

          <Inspector logs={logs} onClear={onClearLogs} onRefresh={onRefreshAll} />
        </div>
      </div>
    </main>
  );
};
