import React from 'react';
import type { HistoryItem } from '../types';
import { Link } from 'react-router-dom';

interface HistorySectionProps {
  historyList: HistoryItem[];
}

export const HistorySection: React.FC<HistorySectionProps> = ({ historyList }) => {
  return (
    <section id="history" className="content-card">
      <div className="card-header">
        <div className="header-left">
          <span className="section-icon">📜</span>
          <h2 className="card-title">회사 연혁 (History)</h2>
        </div>
        <div className="header-right">
          <Link to="/history" className="btn btn-outline btn-xs">
            상세 페이지 ➔
          </Link>
        </div>
      </div>

      <div className="card-body">
        <div className="timeline-container">
          {historyList.length === 0 ? (
            <div className="empty-timeline-msg">등록된 회사 연혁이 없습니다.</div>
          ) : (
            historyList.map((item, idx) => (
              <div key={item.id ?? idx} className="timeline-item">
                <div className="timeline-date">{item.data || item.date || '-'}</div>
                <div className="timeline-content">{item.content}</div>
              </div>
            ))
          )}
        </div>
      </div>
    </section>
  );
};
