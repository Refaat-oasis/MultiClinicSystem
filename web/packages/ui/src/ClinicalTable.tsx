import React from 'react';

export interface Column<T> {
  key: string;
  header: React.ReactNode;
  render?: (item: T, index: number) => React.ReactNode;
  width?: string;
  align?: 'start' | 'center' | 'end';
}

export interface ClinicalTableProps<T> {
  columns: Column<T>[];
  data: T[];
  keyExtractor: (item: T, index: number) => string | number;
  emptyMessage?: string;
  className?: string;
}

export function ClinicalTable<T>({
  columns,
  data,
  keyExtractor,
  emptyMessage = 'لا توجد بيانات متاحة حالياً',
  className = '',
}: ClinicalTableProps<T>) {
  return (
    <div className={`clinical-table-container ${className}`.trim()}>
      <table className="clinical-table">
        <thead>
          <tr>
            {columns.map((col) => (
              <th
                key={col.key}
                style={{
                  width: col.width,
                  textAlign: col.align || 'start',
                }}
              >
                {col.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.length === 0 ? (
            <tr>
              <td
                colSpan={columns.length}
                style={{
                  textAlign: 'center',
                  padding: '2rem 1rem',
                  color: 'var(--color-text-secondary)',
                }}
              >
                {emptyMessage}
              </td>
            </tr>
          ) : (
            data.map((item, index) => (
              <tr key={keyExtractor(item, index)}>
                {columns.map((col) => {
                  const content = col.render ? col.render(item, index) : (item as any)[col.key];
                  return (
                    <td
                      key={col.key}
                      style={{
                        textAlign: col.align || 'start',
                      }}
                    >
                      {content}
                    </td>
                  );
                })}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
