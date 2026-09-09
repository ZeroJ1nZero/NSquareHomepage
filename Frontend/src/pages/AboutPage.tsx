import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import type { AboutData, CurrentUser, LogEntry } from '../types';
import { PermissionBanner } from '../components/PermissionBanner';
import { Inspector } from '../components/Inspector';
import { checkServicePermission } from '../utils/authUtils';

interface AboutPageProps {
  user: CurrentUser;
  about: AboutData;
  logs: LogEntry[];
  onSaveAbout: (content: string) => Promise<void>;
  onOpenLogin: () => void;
  onClearLogs: () => void;
  onRefreshAll: () => Promise<void>;
  showToast: (msg: string, type?: 'info' | 'success' | 'error') => void;
  addLog: (tag: string, msg: string, type?: 'info' | 'success' | 'warn' | 'error') => void;
  formatDate: (date?: string | null) => string;
}

export const AboutPage: React.FC<AboutPageProps> = ({
  user,
  about,
  logs,
  onSaveAbout,
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
    // 🛡️ 회사 소개 세션 쿠키 권한 검사 수행 (미보유 시 302 리다이렉트를 통한 SSO 쿠키 확인 & 세션 발급)
    const hasPermission = checkServicePermission(
      user,
      'about',
      addLog,
      '회사 소개 수정'
    );

    if (!hasPermission) {
      showToast('회사 소개 수정 권한이 없습니다.', 'error');
      return;
    }

    setEditContent(about.content || about.introduction || '');
    setIsEditing(true);
  };

  const handleCancel = () => {
    setIsEditing(false);
  };

  const handleSave = async () => {
    // 🛡️ 저장 전 권한 재검증
    const hasPermission = checkServicePermission(
      user,
      'about',
      addLog,
      '수정 내용 저장'
    );

    if (!hasPermission) {
      showToast('저장 권한이 없습니다.', 'error');
      return;
    }

    if (!editContent.trim()) {
      showToast('소개 내용을 입력해 주세요.', 'error');
      return;
    }

    setIsSaving(true);
    try {
      await onSaveAbout(editContent.trim());
      setIsEditing(false);
    } finally {
      setIsSaving(false);
    }
  };

  const displayText = about.content || about.introduction || '(등록된 회사 소개가 없습니다. 관리자로 로그인하여 등록하세요)';

  return (
    <main className="main-content">
      <div className="container">
        {/* Breadcrumb & Title */}
        <div className="page-header">
          <div className="breadcrumb">
            <Link to="/">홈</Link> <span className="separator">/</span> <span className="current">회사 소개</span>
          </div>
          <div className="page-title-wrap">
            <h1 className="page-title">🏢 회사 소개 (About Company)</h1>
            <p className="page-subtitle">엔스퀘어(N-SQUARE)의 핵심 가치와 비전을 소개합니다.</p>
          </div>
        </div>

        {/* Permission Check Banner */}
        <PermissionBanner user={user} pageName="회사 소개" serviceKey="about" onOpenLogin={onOpenLogin} />

        <div className="content-grid" style={{ marginTop: '24px' }}>
          <div className="data-column">
            <div className="content-card featured-card">
              <div className="card-header">
                <div className="header-left">
                  <span className="section-icon">🏢</span>
                  <h2 className="card-title">엔스퀘어 기업 소개</h2>
                </div>
                <div className="header-right">
                  <span className="track-badge track-a">트랙 A: 공개 조회</span>
                  {!isEditing && (
                    <button onClick={handleStartEdit} className="btn btn-primary btn-sm">
                      ✏️ 소개 수정하기 (권한 검사)
                    </button>
                  )}
                </div>
              </div>

              <div className="card-body">
                {!isEditing ? (
                  <div className="data-view-box">
                    <div className="about-hero-box">
                      <div className="about-quote-mark">“</div>
                      <p className="data-text about-highlight-text">{displayText}</p>
                    </div>

                    <div className="feature-grid" style={{ marginTop: '24px', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                      <div className="feature-item" style={{ background: '#f8fafc', padding: '16px', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
                        <div style={{ fontSize: '20px', marginBottom: '8px' }}>🎯 미션 (Mission)</div>
                        <p style={{ fontSize: '14px', color: '#64748b', margin: 0 }}>Clean Architecture와 최신 MSA 기술을 결합하여 기업 내 생산성을 혁신합니다.</p>
                      </div>
                      <div className="feature-item" style={{ background: '#f8fafc', padding: '16px', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
                        <div style={{ fontSize: '20px', marginBottom: '8px' }}>🌐 비전 (Vision)</div>
                        <p style={{ fontSize: '14px', color: '#64748b', margin: 0 }}>Zero-Trust 기반의 안전하고 확장 가능한 차세대 엔터프라이즈 에코시스템을 선도합니다.</p>
                      </div>
                    </div>

                    <div className="data-meta" style={{ marginTop: '20px' }}>
                      <span className="meta-label">최종 갱신 일시:</span>
                      <span className="meta-val">{formatDate(about.updatedAt)}</span>
                    </div>
                  </div>
                ) : (
                  <div className="data-edit-box">
                    <div className="edit-box-header" style={{ marginBottom: '12px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                      <label htmlFor="inputAboutPage" className="form-label" style={{ margin: 0 }}>
                        회사 소개 본문 수정 (관리자 권한 확인됨)
                      </label>
                      <span className="badge badge-success">Admin Verified</span>
                    </div>
                    <textarea
                      id="inputAboutPage"
                      className="form-textarea"
                      rows={8}
                      value={editContent}
                      onChange={(e) => setEditContent(e.target.value)}
                      placeholder="새로운 회사 소개 내용을 작성하세요..."
                    />
                    <div className="form-actions">
                      <button onClick={handleCancel} className="btn btn-secondary btn-sm" disabled={isSaving}>
                        취소
                      </button>
                      <button onClick={handleSave} className="btn btn-primary btn-sm" disabled={isSaving}>
                        <span className="icon">💾</span> {isSaving ? '저장 중...' : '수정 내용 저장 (PUT)'}
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
