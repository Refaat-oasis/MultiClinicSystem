import React from 'react';
import { TrendingUp, TrendingDown } from 'lucide-react';

export interface StatCardProps {
  title: string;
  value: string | number;
  subtitle?: string;
  trend?: {
    direction: 'up' | 'down';
    value: string;
  };
  icon?: React.ReactNode;
}

export const StatCard: React.FC<StatCardProps> = ({
  title,
  value,
  subtitle,
  trend,
  icon,
}) => {
  return (
    <div className="admin-card">
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.75rem' }}>
        <span style={{ fontSize: '0.875rem', fontWeight: 600, color: 'var(--admin-text-secondary)' }}>
          {title}
        </span>
        {icon && (
          <div
            style={{
              width: '2.5rem',
              height: '2.5rem',
              borderRadius: 'var(--admin-radius-input)',
              backgroundColor: 'var(--admin-accent-soft)',
              color: 'var(--admin-accent-primary)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            {icon}
          </div>
        )}
      </div>

      <div style={{ display: 'flex', alignItems: 'baseline', gap: '0.75rem', marginBottom: '0.25rem' }}>
        <span
          className="num-tabular"
          style={{
            fontSize: '1.75rem',
            fontWeight: 700,
            color: 'var(--admin-text-primary)',
          }}
        >
          {value}
        </span>

        {trend && (
          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '0.25rem',
              fontSize: '0.75rem',
              fontWeight: 600,
              color: trend.direction === 'up' ? 'var(--admin-status-success)' : 'var(--admin-status-danger)',
            }}
          >
            {trend.direction === 'up' ? <TrendingUp size={14} /> : <TrendingDown size={14} />}
            <span>{trend.value}</span>
          </span>
        )}
      </div>

      {subtitle && (
        <p style={{ fontSize: '0.75rem', color: 'var(--admin-text-muted)' }}>
          {subtitle}
        </p>
      )}
    </div>
  );
};
