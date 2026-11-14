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
    <span className={`clinic-chip clinic-chip-${status}`}>
      {showDot && <span className="clinic-chip-dot" aria-hidden="true" />}
      <span>{label}</span>
    </span>
  );
};
