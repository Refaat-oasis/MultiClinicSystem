import React from 'react';
import { ThemeToggle } from './ThemeToggle';
import { LanguageSwitcher } from './LanguageSwitcher';

export interface ClinicHeaderProps {
  title: string;
  subtitle?: string;
  badge?: string;
  brandIcon?: React.ReactNode;
  rightContent?: React.ReactNode;
  className?: string;
}

export const ClinicHeader: React.FC<ClinicHeaderProps> = ({
  title,
  subtitle,
  badge = 'v1.0',
  brandIcon,
  rightContent,
  className = '',
}) => {
  return (
    <header
      className={`glass-card ${className}`.trim()}
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0.875rem 1.5rem',
        marginBottom: '1.5rem',
        flexWrap: 'wrap',
        gap: '1rem',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
        {brandIcon && (
          <div
            style={{
              width: '2.5rem',
              height: '2.5rem',
              borderRadius: 'var(--radius-input)',
              backgroundColor: 'var(--color-accent-teal)',
              color: '#ffffff',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              boxShadow: 'var(--glass-shadow)',
              flexShrink: 0,
            }}
          >
            {brandIcon}
          </div>
        )}
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <h1 style={{ fontSize: '1.125rem', fontWeight: 700, margin: 0, color: 'var(--color-text-primary)' }}>
              {title}
            </h1>
            {badge && (
              <span
                style={{
                  fontSize: '0.6875rem',
                  fontFamily: 'monospace',
                  fontWeight: 600,
                  paddingInline: '0.5rem',
                  paddingBlock: '0.125rem',
                  borderRadius: 'var(--radius-pill)',
                  backgroundColor: 'var(--color-accent-teal-soft)',
                  color: 'var(--color-accent-teal-text)',
                }}
              >
                {badge}
              </span>
            )}
          </div>
          {subtitle && (
            <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-secondary)', margin: '0.125rem 0 0' }}>
              {subtitle}
            </p>
          )}
        </div>
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap' }}>
        {rightContent}
        <ThemeToggle />
        <LanguageSwitcher />
      </div>
    </header>
  );
};
