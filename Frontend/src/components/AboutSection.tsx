import React from 'react';
import type { AboutData } from '../types';
import { Link } from 'react-router-dom';

interface AboutSectionProps {
  about: AboutData;
  formatDate: (date?: string | null) => string;
}

export const AboutSection: React.FC<AboutSectionProps> = ({ about, formatDate }) => {
  const displayText = about.content || about.introduction || '(등록된 회사 소개가 없습니다)';

  return (
    <section id="about" className="content-card">
      <div className="card-header">
        <div className="header-left">
          <span className="section-icon">🏢</span>
          <h2 className="card-title">회사 소개 (About)</h2>
        </div>
        <div className="header-right">
          <Link to="/about" className="btn btn-outline btn-xs">
            상세 페이지 ➔
          </Link>
        </div>
      </div>

      <div className="card-body">
        <div className="data-view-box">
          <p className="data-text">{displayText}</p>
          <div className="data-meta">
            <span className="meta-label">최종 갱신:</span>
            <span className="meta-val">{formatDate(about.updatedAt)}</span>
          </div>
        </div>
      </div>
    </section>
  );
};
