import React from 'react';

export interface Column<T> {
  key: string;
  header: string;
  render?: (item: T) => React.ReactNode;
  align?: 'right' | 'left' | 'center';
}

export interface ClinicalTableProps<T> {
  columns: Column<T>[];
  data: T[];
  keyExtractor: (item: T) => string;
  emptyMessage?: string;
}

export function ClinicalTable<T>({
  columns,
  data,
  keyExtractor,
  emptyMessage = 'لا توجد بيانات متاحة حالياً',
}: ClinicalTableProps<T>) {
  if (data.length === 0) {
    return (
      <div
        className="admin-card"
        style={{
          textAlign: 'center',
          padding: '2.5rem 1rem',
          color: 'var(--admin-text-secondary)',
        }}
      >
        <p style={{ fontSize: '0.875rem' }}>{emptyMessage}</p>
      </div>
    );
  }

  return (
    <div style={{ overflowX: 'auto', borderRadius: 'var(--admin-radius-card)', border: '1px solid var(--admin-border-subtle)' }}>
      <table className="admin-table">
        <thead>
          <tr>
            {columns.map((col) => (
              <th key={col.key} style={{ textAlign: col.align || 'right' }}>
                {col.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.map((item) => (
            <tr key={keyExtractor(item)}>
              {columns.map((col) => (
                <td key={col.key} style={{ textAlign: col.align || 'right' }}>
                  {col.render ? col.render(item) : (item as Record<string, unknown>)[col.key] as React.ReactNode}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
