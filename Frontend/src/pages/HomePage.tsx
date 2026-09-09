import React from 'react';
import { Link } from 'react-router-dom';
import type { AboutData, CurrentUser, HistoryItem, LogEntry, ServiceData } from '../types';
import { HeroSection } from '../components/HeroSection';
import { AboutSection } from '../components/AboutSection';
import { ServiceSection } from '../components/ServiceSection';
import { HistorySection } from '../components/HistorySection';
import { Inspector } from '../components/Inspector';

interface HomePageProps {
  user: CurrentUser;
  about: AboutData;
  service: ServiceData;
  histories: HistoryItem[];
  logs: LogEntry[];
  onClearLogs: () => void;
  onRefreshAll: () => Promise<void>;
  formatDate: (date?: string | null) => string;
}

export const HomePage: React.FC<HomePageProps> = ({
  user,
  about,
  service,
  histories,
  logs,
  onClearLogs,
  onRefreshAll,
  formatDate,
}) => {
  return (
    <>
      <HeroSection user={user} />

      <main className="main-content">
        <div className="container">
          {/* Quick Navigation Cards */}
          <div className="quick-nav-grid" style={{ marginBottom: '32px' }}>
            <div className="quick-nav-card">
              <div className="quick-card-icon">🏢</div>
              <div className="quick-card-body">
                <h3>회사 소개</h3>
                <p>N-SQUARE의 비전과 소개 정보를 확인합니다.</p>
                <Link to="/about" className="quick-nav-link">
                  소개 상세/관리 ➔
                </Link>
              </div>
            </div>

            <div className="quick-nav-card">
              <div className="quick-card-icon">⚡</div>
              <div className="quick-card-body">
                <h3>주요 서비스</h3>
                <p>엔터프라이즈 통합 플랫폼의 핵심 기능과 기술 서비스를 확인합니다.</p>
                <Link to="/service" className="quick-nav-link">
                  서비스 상세/관리 ➔
                </Link>
              </div>
            </div>

            <div className="quick-nav-card">
              <div className="quick-card-icon">📜</div>
              <div className="quick-card-body">
                <h3>회사 연혁</h3>
                <p>엔스퀘어의 설립부터 현재까지의 주요 성장 타임라인을 확인합니다.</p>
                <Link to="/history" className="quick-nav-link">
                  연혁 상세/관리 ➔
                </Link>
              </div>
            </div>
          </div>

          <div className="content-grid">
            <div className="data-column">
              <AboutSection
                about={about}
                formatDate={formatDate}
              />

              <ServiceSection
                service={service}
                formatDate={formatDate}
              />

              <HistorySection
                historyList={histories}
              />
            </div>

            <Inspector
              logs={logs}
              onClear={onClearLogs}
              onRefresh={onRefreshAll}
            />
          </div>
        </div>
      </main>
    </>
  );
};
