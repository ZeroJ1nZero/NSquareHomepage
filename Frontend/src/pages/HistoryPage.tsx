import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import type { CurrentUser, HistoryItem, LogEntry } from '../types';
import { PermissionBanner } from '../components/PermissionBanner';
import { Inspector } from '../components/Inspector';
import { checkServicePermission } from '../utils/authUtils';

interface HistoryPageProps {
  user: CurrentUser;
  histories: HistoryItem[];
  logs: LogEntry[];
  onAddHistory: (date: string, content: string) => Promise<void>;
  onDeleteHistory: (id: number) => Promise<void>;
  onOpenLogin: () => void;
  onClearLogs: () => void;
  onRefreshAll: () => Promise<void>;
  showToast: (msg: string, type?: 'info' | 'success' | 'error') => void;
  addLog: (tag: string, msg: string, type?: 'info' | 'success' | 'warn' | 'error') => void;
}

export const HistoryPage: React.FC<HistoryPageProps> = ({
  user,
  histories,
  logs,
  onAddHistory,
  onDeleteHistory,
  onOpenLogin,
  onClearLogs,
  onRefreshAll,
  showToast,
  addLog,
}) => {
  const [showAddForm, setShowAddForm] = useState(false);
  const [addDate, setAddDate] = useState(new Date().toISOString().split('T')[0]);
  const [addContent, setAddContent] = useState('');
  const [isAdding, setIsAdding] = useState(false);

  const handleToggleAddForm = () => {
    // 🛡️ 연혁 추가 폼 열기 전 권한 검사 (미보유 시 302 리다이렉트를 통한 SSO 쿠키 확인 & 세션 발급)
    const hasPermission = checkServicePermission(
      user,
      'history',
      addLog,
      '연혁 추가 폼 열기'
    );

    if (!hasPermission) {
      return;
    }

    setShowAddForm(!showAddForm);
  };

  const handleAdd = async () => {
    // 🛡️ 저장 전 권한 재검증
    const hasPermission = checkServicePermission(
      user,
      'history',
      addLog,
      '연혁 추가'
    );

    if (!hasPermission) {
      return;
    }

    if (!addDate || !addContent.trim()) {
      showToast('날짜와 내용을 모두 입력해 주세요.', 'error');
      return;
    }

    setIsAdding(true);
    try {
      await onAddHistory(addDate, addContent.trim());
      setAddContent('');
      setShowAddForm(false);
    } finally {
      setIsAdding(false);
    }
  };

  const handleDelete = async (id: number) => {
    // 🛡️ 삭제 전 권한 검증
    const hasPermission = checkServicePermission(
      user,
      'history',
      addLog,
      `연혁 항목(ID: ${id}) 삭제`
    );

    if (!hasPermission) {
      return;
    }

    if (!confirm(`이 연혁 항목(ID: ${id})을 삭제하시겠습니까?`)) {
      return;
    }

    await onDeleteHistory(id);
  };

  return (
    <main className="main-content">
      <div className="container">
        {/* Breadcrumb & Title */}
        <div className="page-header">
          <div className="breadcrumb">
            <Link to="/">홈</Link> <span className="separator">/</span> <span className="current">회사 연혁</span>
          </div>
          <div className="page-title-wrap">
            <h1 className="page-title">📜 회사 연혁 (Company History)</h1>
            <p className="page-subtitle">엔스퀘어의 설립과 도전, 주요 성장 발자취입니다.</p>
          </div>
        </div>

        {/* Permission Check Banner */}
        <PermissionBanner user={user} pageName="회사 연혁" serviceKey="history" onOpenLogin={onOpenLogin} />

        <div className="content-grid" style={{ marginTop: '24px' }}>
          <div className="data-column">
            <div className="content-card featured-card">
              <div className="card-header">
                <div className="header-left">
                  <span className="section-icon">📜</span>
                  <h2 className="card-title">주요 연혁 및 마일스톤 ({histories.length}건)</h2>
                </div>
                <div className="header-right">
                  <span className="track-badge track-a">트랙 A: 공개 조회</span>
                  <button onClick={handleToggleAddForm} className="btn btn-primary btn-sm">
                    {showAddForm ? '닫기' : '➕ 연혁 추가 (권한 검사)'}
                  </button>
                </div>
              </div>

              <div className="card-body">
                {showAddForm && (
                  <div className="data-edit-box" style={{ marginBottom: '24px' }}>
                    <div className="edit-box-header" style={{ marginBottom: '12px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                      <label className="form-label" style={{ margin: 0 }}>
                        새 연혁 마일스톤 추가 (관리자 권한 확인됨)
                      </label>
                      <span className="badge badge-success">Admin Verified</span>
                    </div>
                    <div className="form-grid">
                      <div className="form-group">
                        <label htmlFor="inputHistoryDatePage" className="form-label">연혁 날짜</label>
                        <input
                          type="date"
                          id="inputHistoryDatePage"
                          className="form-input"
                          value={addDate}
                          onChange={(e) => setAddDate(e.target.value)}
                        />
                      </div>
                      <div className="form-group">
                        <label htmlFor="inputHistoryContentPage" className="form-label">연혁 내용</label>
                        <input
                          type="text"
                          id="inputHistoryContentPage"
                          className="form-input"
                          value={addContent}
                          onChange={(e) => setAddContent(e.target.value)}
                          placeholder="연혁 세부 내용을 입력하세요..."
                        />
                      </div>
                    </div>
                    <div className="form-actions" style={{ marginTop: '16px' }}>
                      <button onClick={() => setShowAddForm(false)} className="btn btn-secondary btn-sm" disabled={isAdding}>
                        취소
                      </button>
                      <button onClick={handleAdd} className="btn btn-primary btn-sm" disabled={isAdding}>
                        <span className="icon">➕</span> {isAdding ? '추가 중...' : '연혁 항목 저장 (PUT)'}
                      </button>
                    </div>
                  </div>
                )}

                <div className="timeline-container">
                  {histories.length === 0 ? (
                    <div className="empty-timeline-msg">등록된 회사 연혁이 없습니다.</div>
                  ) : (
                    histories.map((item, idx) => (
                      <div key={item.id ?? idx} className="timeline-item" style={{ position: 'relative' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                          <div className="timeline-date">{item.data || item.date || '-'}</div>
                          {item.id !== undefined && (
                            <button
                              onClick={() => handleDelete(item.id!)}
                              className="btn btn-danger btn-xs"
                              style={{
                                padding: '3px 8px',
                                fontSize: '12px',
                                borderRadius: '4px',
                                background: '#ef4444',
                                color: 'white',
                                border: 'none',
                                cursor: 'pointer',
                              }}
                              title="삭제 전 관리자 권한을 체크합니다"
                            >
                              🗑️ 삭제 (권한 검사)
                            </button>
                          )}
                        </div>
                        <div className="timeline-content">{item.content}</div>
                      </div>
                    ))
                  )}
                </div>
              </div>
            </div>
          </div>

          <Inspector logs={logs} onClear={onClearLogs} onRefresh={onRefreshAll} />
        </div>
      </div>
    </main>
  );
};
