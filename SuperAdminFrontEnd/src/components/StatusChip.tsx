import React from 'react';

export type StatusVariant = 'success' | 'warning' | 'danger' | 'info';

export interface StatusChipProps {
  status: StatusVariant;
  label: string;
  showDot?: boolean;
}

export const StatusChip: React.FC<StatusChipProps> = ({
  status,
  label,
  showDot = true,
}) => {
  return (
    <span className={`admin-chip admin-chip-${status}`}>
      {showDot && <span className="admin-chip-dot" aria-hidden="true" />}
      <span>{label}</span>
    </span>
  );
};
