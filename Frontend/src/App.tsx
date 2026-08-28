import React, { useState, useEffect, useCallback } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import type { CurrentUser, AboutData, ServiceData, HistoryItem, LogEntry, ToastMessage } from './types';
import * as api from './api';
import { Header } from './components/Header';
import { HomePage } from './pages/HomePage';
import { AboutPage } from './pages/AboutPage';
import { ServicePage } from './pages/ServicePage';
import { HistoryPage } from './pages/HistoryPage';
import { LoginPage } from './pages/LoginPage';
import { CallbackPage } from './pages/CallbackPage';
import { ToastContainer } from './components/ToastContainer';

export const App: React.FC = () => {
  const [user, setUser] = useState<CurrentUser>({
    isAuthenticated: false,
    userName: null,
    role: null,
  });

  const [about, setAbout] = useState<AboutData>({});
  const [service, setService] = useState<ServiceData>({});
  const [histories, setHistories] = useState<HistoryItem[]>([]);
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const addLog = useCallback((tag: string, msg: string, type: 'info' | 'success' | 'warn' | 'error' = 'info') => {
    const timeStr = new Date().toTimeString().split(' ')[0];
    const newEntry: LogEntry = {
      id: `${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,
      time: timeStr,
      tag,
      msg,
      type,
    };
    setLogs((prev) => [...prev, newEntry]);
  }, []);

  const showToast = useCallback((message: string, type: 'info' | 'success' | 'error' = 'info') => {
    const id = `${Date.now()}-${Math.random().toString(36).substring(2, 7)}`;
    setToasts((prev) => [...prev, { id, message, type }]);
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, 3000);
  }, []);

  const formatDate = (isoStr?: string | null) => {
    if (!isoStr) return '-';
    try {
      const d = new Date(isoStr);
      return isNaN(d.getTime()) ? isoStr : d.toLocaleString('ko-KR');
    } catch {
      return isoStr;
    }
  };

  const checkAuth = useCallback(async () => {
    addLog('AUTH', '서비스 세션 쿠키(.NsqHomepage.ServiceSession) 및 인증 상태 확인 중...');
    try {
      const u = await api.getCurrentUser();
      setUser(u);
      addLog('AUTH', `로그인 인증 확인 완료 - 사용자: ${u.userName}, 권한: ${u.role}`, 'success');
    } catch {
      setUser({ isAuthenticated: false, userName: null, role: null });
      addLog('AUTH', '비로그인 상태 (일반 방문자 - 트랙 A 공개 조회 모드)', 'info');
    }
  }, [addLog]);

  const loadAbout = useCallback(async () => {
    addLog('트랙 A', '회사 소개 정보 조회 (GET /api/public/company-about) -> Gateway 대행 호출');
    try {
      const data = await api.getCompanyAbout();
      setAbout(data);
      addLog('트랙 A', '회사 소개 데이터 수신 완료 (200 OK)', 'success');
    } catch (err: any) {
      addLog('트랙 A', `회사 소개 조회 실패: ${err.message}`, 'error');
    }
  }, [addLog]);

  const loadService = useCallback(async () => {
    addLog('트랙 A', '회사 서비스 정보 조회 (GET /api/public/company-services) -> Gateway 대행 호출');
    try {
      const data = await api.getCompanyServices();
      setService(data);
      addLog('트랙 A', '회사 서비스 데이터 수신 완료 (200 OK)', 'success');
    } catch (err: any) {
      addLog('트랙 A', `회사 서비스 조회 실패: ${err.message}`, 'error');
    }
  }, [addLog]);

  const loadHistories = useCallback(async () => {
    addLog('트랙 A', '회사 전체 연혁 목록 조회 (GET /api/public/company-histories) -> Gateway 대행 호출');
    try {
      const list = await api.getCompanyHistories();
      setHistories(list);
      addLog('트랙 A', `연혁 데이터 수신 완료 (${list.length}건, 200 OK)`, 'success');
    } catch (err: any) {
      addLog('트랙 A', `연혁 조회 실패: ${err.message}`, 'error');
    }
  }, [addLog]);

  const loadAll = useCallback(async () => {
    await Promise.all([loadAbout(), loadService(), loadHistories()]);
  }, [loadAbout, loadService, loadHistories]);

  useEffect(() => {
    addLog('SYSTEM', 'React 프론트엔드 라우터 및 데이터 초기화 완료.', 'info');
    checkAuth();
    loadAll();
  }, [checkAuth, loadAll, addLog]);

  const handleStartSso = () => {
    addLog('AUTH', `[SSO 시작] ServiceServer Step 01 (/api/auth/access-sso) 호출 및 AuthServer 인가 페이지로 이동`);
    api.startSso(window.location.href);
  };

  const handleLogout = async () => {
    addLog('AUTH', '서비스 세션 쿠키 삭제 및 로그아웃 완료');
    await api.logout();
    showToast('성공적으로 로그아웃되었습니다.', 'info');
    await checkAuth();
  };

  const handleSaveAbout = async (content: string) => {
    addLog('트랙 B', '회사 소개 수정 요청 전송 (PUT /api/admin/company-about)');
    try {
      const updated = await api.updateCompanyAbout(content);
      setAbout(updated);
      addLog('트랙 B', '회사 소개 수정 성공! ResourceServer DB 반영 (200 OK)', 'success');
      showToast('회사 소개가 성공적으로 수정되었습니다.', 'success');
    } catch (err: any) {
      addLog('트랙 B', `수정 실패: ${err.message}`, 'error');
      showToast(err.message || '수정에 실패했습니다.', 'error');
      if (err.message.includes('401')) {
        handleStartSso();
      }
    }
  };

  const handleSaveService = async (newService: string) => {
    addLog('트랙 B', '서비스 정보 수정 요청 전송 (PUT /api/admin/company-services)');
    try {
      const updated = await api.updateCompanyServices(newService);
      setService(updated);
      addLog('트랙 B', '서비스 정보 수정 성공! ResourceServer DB 반영 (200 OK)', 'success');
      showToast('서비스 정보가 성공적으로 수정되었습니다.', 'success');
    } catch (err: any) {
      addLog('트랙 B', `수정 실패: ${err.message}`, 'error');
      showToast(err.message || '수정에 실패했습니다.', 'error');
      if (err.message.includes('401')) {
        handleStartSso();
      }
    }
  };

  const handleAddHistory = async (date: string, content: string) => {
    addLog('트랙 B', `새 연혁 항목 추가 요청 (PUT /api/admin/company-histories, [${date}] ${content})`);
    try {
      const updated = await api.addCompanyHistory(date, content);
      setHistories(updated);
      addLog('트랙 B', '연혁 항목 저장 성공! ResourceServer DB 반영 (200 OK)', 'success');
      showToast('연혁 항목이 성공적으로 추가되었습니다.', 'success');
    } catch (err: any) {
      addLog('트랙 B', `저장 실패: ${err.message}`, 'error');
      showToast(err.message || '저장에 실패했습니다.', 'error');
      if (err.message.includes('401')) {
        handleStartSso();
      }
    }
  };

  const handleDeleteHistory = async (id: number) => {
    addLog('트랙 B', `연혁 항목 삭제 요청 (DELETE /api/admin/company-histories/${id})`);
    try {
      await api.deleteCompanyHistory(id);
      addLog('트랙 B', '연혁 삭제 완료! ResourceServer DB 반영 (200 OK)', 'success');
      showToast('연혁 항목이 삭제되었습니다.', 'success');
      await loadHistories();
    } catch (err: any) {
      addLog('트랙 B', `삭제 실패: ${err.message}`, 'error');
      showToast(err.message || '삭제에 실패했습니다.', 'error');
      if (err.message.includes('401')) {
        handleStartSso();
      }
    }
  };

  return (
    <BrowserRouter>
      <div className="app-root">
        <Header
          user={user}
          onOpenLogin={handleStartSso}
          onLogout={handleLogout}
        />

        <Routes>
          <Route
            path="/"
            element={
              <HomePage
                user={user}
                about={about}
                service={service}
                histories={histories}
                logs={logs}
                onClearLogs={() => {
                  setLogs([]);
                  addLog('SYSTEM', '로그 콘솔이 초기화되었습니다.', 'info');
                }}
                onRefreshAll={async () => {
                  addLog('SYSTEM', '전체 데이터 및 세션 상태를 새로고침합니다...');
                  await checkAuth();
                  await loadAll();
                  showToast('데이터를 새로고침했습니다.', 'info');
                }}
                formatDate={formatDate}
              />
            }
          />
          <Route
            path="/about"
            element={
              <AboutPage
                user={user}
                about={about}
                logs={logs}
                onSaveAbout={handleSaveAbout}
                onOpenLogin={handleStartSso}
                onClearLogs={() => {
                  setLogs([]);
                  addLog('SYSTEM', '로그 콘솔이 초기화되었습니다.', 'info');
                }}
                onRefreshAll={async () => {
                  addLog('SYSTEM', '회사 소개 데이터를 새로고침합니다...');
                  await checkAuth();
                  await loadAbout();
                  showToast('회사 소개 데이터를 새로고침했습니다.', 'info');
                }}
                showToast={showToast}
                addLog={addLog}
                formatDate={formatDate}
              />
            }
          />
          <Route
            path="/service"
            element={
              <ServicePage
                user={user}
                service={service}
                logs={logs}
                onSaveService={handleSaveService}
                onOpenLogin={handleStartSso}
                onClearLogs={() => {
                  setLogs([]);
                  addLog('SYSTEM', '로그 콘솔이 초기화되었습니다.', 'info');
                }}
                onRefreshAll={async () => {
                  addLog('SYSTEM', '서비스 데이터를 새로고침합니다...');
                  await checkAuth();
                  await loadService();
                  showToast('서비스 데이터를 새로고침했습니다.', 'info');
                }}
                showToast={showToast}
                addLog={addLog}
                formatDate={formatDate}
              />
            }
          />
          <Route
            path="/history"
            element={
              <HistoryPage
                user={user}
                histories={histories}
                logs={logs}
                onAddHistory={handleAddHistory}
                onDeleteHistory={handleDeleteHistory}
                onOpenLogin={handleStartSso}
                onClearLogs={() => {
                  setLogs([]);
                  addLog('SYSTEM', '로그 콘솔이 초기화되었습니다.', 'info');
                }}
                onRefreshAll={async () => {
                  addLog('SYSTEM', '연혁 데이터를 새로고침합니다...');
                  await checkAuth();
                  await loadHistories();
                  showToast('연혁 데이터를 새로고침했습니다.', 'info');
                }}
                showToast={showToast}
                addLog={addLog}
              />
            }
          />
          <Route
            path="/login"
            element={<LoginPage />}
          />
          <Route
            path="/callback"
            element={<CallbackPage />}
          />
          <Route
            path="/signin-oidc"
            element={<CallbackPage />}
          />
        </Routes>

        <ToastContainer toasts={toasts} />
      </div>
    </BrowserRouter>
  );
};
export default App;
