import React from 'react';

export type StatusChipVariant =
  | 'success'
  | 'confirmed'
  | 'warning'
  | 'pending'
  | 'danger'
  | 'cancelled'
  | 'info'
  | 'consultation';

export interface StatusChipProps extends React.HTMLAttributes<HTMLSpanElement> {
  status: StatusChipVariant;
  label: string;
  showDot?: boolean;
  className?: string;
}

export const StatusChip: React.FC<StatusChipProps> = ({
  status,
  label,
  showDot = true,
  className = '',
  ...props
}) => {
  let mappedClass = 'status-chip-info';

  if (status === 'success' || status === 'confirmed') {
    mappedClass = 'status-chip-success';
  } else if (status === 'warning' || status === 'pending') {
    mappedClass = 'status-chip-warning';
  } else if (status === 'danger' || status === 'cancelled') {
    mappedClass = 'status-chip-danger';
  }

  return (
    <span className={`status-chip ${mappedClass} ${className}`.trim()} {...props}>
      {showDot && <span className="status-chip-dot" aria-hidden="true" />}
      <span>{label}</span>
    </span>
  );
};
