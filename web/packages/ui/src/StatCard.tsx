import React from 'react';
import { GlassCard } from './GlassCard';

export interface StatCardProps {
  title: string;
  value: string | number;
  subtitle?: string;
  trend?: {
    value: string;
    isPositive?: boolean;
  };
  icon?: React.ReactNode;
  iconBg?: string;
  className?: string;
  onClick?: () => void;
}

export const StatCard: React.FC<StatCardProps> = ({
  title,
  value,
  subtitle,
  trend,
  icon,
  iconBg,
  className = '',
  onClick,
}) => {
  return (
    <GlassCard
      className={`stat-card ${className}`.trim()}
      onClick={onClick}
      style={{
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        cursor: onClick ? 'pointer' : 'default',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '0.5rem', marginBottom: '0.75rem' }}>
        <div style={{ display: 'flex', flexDirection: 'column' }}>
          <span style={{ fontSize: '0.75rem', fontWeight: 700, textTransform: 'uppercase', color: 'var(--color-text-secondary)', letterSpacing: '0.025em' }}>
            {title}
          </span>
          <div style={{ fontSize: '1.75rem', fontWeight: 700, color: 'var(--color-text-primary)', marginTop: '0.25rem' }}>
            <span className="force-ltr" style={{ fontFamily: 'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace' }}>
              {value}
            </span>
          </div>
        </div>

        {icon && (
          <div
            style={{
              width: '2.5rem',
              height: '2.5rem',
              borderRadius: 'var(--radius-input)',
              backgroundColor: iconBg || 'var(--color-accent-teal-soft)',
              color: 'var(--color-accent-teal)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            {icon}
          </div>
        )}
      </div>

      {(trend || subtitle) && (
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.75rem', marginTop: '0.25rem' }}>
          {trend && (
            <span
              style={{
                color: trend.isPositive ? 'var(--color-status-success)' : 'var(--color-status-danger)',
                fontWeight: 600,
                display: 'inline-flex',
                alignItems: 'center',
                gap: '0.25rem',
              }}
            >
              {trend.value}
            </span>
          )}
          {subtitle && (
            <span style={{ color: 'var(--color-text-secondary)' }}>
              {subtitle}
            </span>
          )}
        </div>
      )}
    </GlassCard>
  );
};
