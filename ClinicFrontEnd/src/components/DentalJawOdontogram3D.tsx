import React, { useState } from 'react';
import { Sparkles } from 'lucide-react';

export type ToothStatus = 'healthy' | 'caries' | 'rct' | 'crown' | 'extracted' | 'implant';
export type ToothSurface = 'occlusal' | 'mesial' | 'distal' | 'buccal' | 'lingual';

export interface ToothData {
  number: number;
  nameAr: string;
  nameEn: string;
  jaw: 'upper' | 'lower';
  quadrant: 1 | 2 | 3 | 4;
  status: ToothStatus;
  affectedSurfaces: ToothSurface[];
  notes?: string;
}

// 32 Adult Teeth Catalog with precise Arabic and English anatomical nomenclature
const INITIAL_TEETH_CATALOG: ToothData[] = [
  // Upper Right (Quadrant 1)
  { number: 18, nameAr: 'ضرس العقل العلوي الأيمن', nameEn: 'Upper Right Third Molar', jaw: 'upper', quadrant: 1, status: 'healthy', affectedSurfaces: [] },
  { number: 17, nameAr: 'الضرس الثاني العلوي الأيمن', nameEn: 'Upper Right Second Molar', jaw: 'upper', quadrant: 1, status: 'healthy', affectedSurfaces: [] },
  { number: 16, nameAr: 'الضرس الأول العلوي الأيمن', nameEn: 'Upper Right First Molar', jaw: 'upper', quadrant: 1, status: 'crown', affectedSurfaces: ['occlusal', 'buccal'], notes: 'تاج زيركون كامل' },
  { number: 15, nameAr: 'الضاحك الثاني العلوي الأيمن', nameEn: 'Upper Right Second Premolar', jaw: 'upper', quadrant: 1, status: 'healthy', affectedSurfaces: [] },
  { number: 14, nameAr: 'الضاحك الأول العلوي الأيمن', nameEn: 'Upper Right First Premolar', jaw: 'upper', quadrant: 1, status: 'healthy', affectedSurfaces: [] },
  { number: 13, nameAr: 'الناب العلوي الأيمن', nameEn: 'Upper Right Canine', jaw: 'upper', quadrant: 1, status: 'healthy', affectedSurfaces: [] },
  { number: 12, nameAr: 'القاطع الجانبي العلوي الأيمن', nameEn: 'Upper Right Lateral Incisor', jaw: 'upper', quadrant: 1, status: 'healthy', affectedSurfaces: [] },
  { number: 11, nameAr: 'القاطع المركزي العلوي الأيمن', nameEn: 'Upper Right Central Incisor', jaw: 'upper', quadrant: 1, status: 'healthy', affectedSurfaces: [] },

  // Upper Left (Quadrant 2)
  { number: 21, nameAr: 'القاطع المركزي العلوي الأيسر', nameEn: 'Upper Left Central Incisor', jaw: 'upper', quadrant: 2, status: 'healthy', affectedSurfaces: [] },
  { number: 22, nameAr: 'القاطع الجانبي العلوي الأيسر', nameEn: 'Upper Left Lateral Incisor', jaw: 'upper', quadrant: 2, status: 'healthy', affectedSurfaces: [] },
  { number: 23, nameAr: 'الناب العلوي الأيسر', nameEn: 'Upper Left Canine', jaw: 'upper', quadrant: 2, status: 'healthy', affectedSurfaces: [] },
  { number: 24, nameAr: 'الضاحك الأول العلوي الأيسر', nameEn: 'Upper Left First Premolar', jaw: 'upper', quadrant: 2, status: 'healthy', affectedSurfaces: [] },
  { number: 25, nameAr: 'الضاحك الثاني العلوي الأيسر', nameEn: 'Upper Left Second Premolar', jaw: 'upper', quadrant: 2, status: 'caries', affectedSurfaces: ['occlusal', 'distal'], notes: 'تسوس طبقة المينا والعاج' },
  { number: 26, nameAr: 'الضرس الأول العلوي الأيسر', nameEn: 'Upper Left First Molar', jaw: 'upper', quadrant: 2, status: 'healthy', affectedSurfaces: [] },
  { number: 27, nameAr: 'الضرس الثاني العلوي الأيسر', nameEn: 'Upper Left Second Molar', jaw: 'upper', quadrant: 2, status: 'healthy', affectedSurfaces: [] },
  { number: 28, nameAr: 'ضرس العقل العلوي الأيسر', nameEn: 'Upper Left Third Molar', jaw: 'upper', quadrant: 2, status: 'extracted', affectedSurfaces: [], notes: 'مخلوع جراحياً' },

  // Lower Left (Quadrant 3)
  { number: 31, nameAr: 'القاطع المركزي السفلي الأيسر', nameEn: 'Lower Left Central Incisor', jaw: 'lower', quadrant: 3, status: 'healthy', affectedSurfaces: [] },
  { number: 32, nameAr: 'القاطع الجانبي السفلي الأيسر', nameEn: 'Lower Left Lateral Incisor', jaw: 'lower', quadrant: 3, status: 'healthy', affectedSurfaces: [] },
  { number: 33, nameAr: 'الناب السفلي الأيسر', nameEn: 'Lower Left Canine', jaw: 'lower', quadrant: 3, status: 'healthy', affectedSurfaces: [] },
  { number: 34, nameAr: 'الضاحك الأول السفلي الأيسر', nameEn: 'Lower Left First Premolar', jaw: 'lower', quadrant: 3, status: 'healthy', affectedSurfaces: [] },
  { number: 35, nameAr: 'الضاحك الثاني السفلي الأيسر', nameEn: 'Lower Left Second Premolar', jaw: 'lower', quadrant: 3, status: 'healthy', affectedSurfaces: [] },
  { number: 36, nameAr: 'الضرس الأول السفلي الأيسر', nameEn: 'Lower Left First Molar', jaw: 'lower', quadrant: 3, status: 'rct', affectedSurfaces: ['occlusal', 'mesial'], notes: 'علاج عصب قيد المتابعة' },
  { number: 37, nameAr: 'الضرس الثاني السفلي الأيسر', nameEn: 'Lower Left Second Molar', jaw: 'lower', quadrant: 3, status: 'healthy', affectedSurfaces: [] },
  { number: 38, nameAr: 'ضرس العقل السفلي الأيسر', nameEn: 'Lower Left Third Molar', jaw: 'lower', quadrant: 3, status: 'healthy', affectedSurfaces: [] },

  // Lower Right (Quadrant 4)
  { number: 48, nameAr: 'ضرس العقل السفلي الأيمن', nameEn: 'Lower Right Third Molar', jaw: 'lower', quadrant: 4, status: 'implant', affectedSurfaces: [], notes: 'زراعة تيتانيوم مدمجة' },
  { number: 47, nameAr: 'الضرس الثاني السفلي الأيمن', nameEn: 'Lower Right Second Molar', jaw: 'lower', quadrant: 4, status: 'healthy', affectedSurfaces: [] },
  { number: 46, nameAr: 'الضرس الأول السفلي الأيمن', nameEn: 'Lower Right First Molar', jaw: 'lower', quadrant: 4, status: 'caries', affectedSurfaces: ['occlusal'], notes: 'نخر إطباقي أولي' },
  { number: 45, nameAr: 'الضاحك الثاني السفلي الأيمن', nameEn: 'Lower Right Second Premolar', jaw: 'lower', quadrant: 4, status: 'healthy', affectedSurfaces: [] },
  { number: 44, nameAr: 'الضاحك الأول السفلي الأيمن', nameEn: 'Lower Right First Premolar', jaw: 'lower', quadrant: 4, status: 'healthy', affectedSurfaces: [] },
  { number: 43, nameAr: 'الناب السفلي الأيمن', nameEn: 'Lower Right Canine', jaw: 'lower', quadrant: 4, status: 'healthy', affectedSurfaces: [] },
  { number: 42, nameAr: 'القاطع الجانبي السفلي الأيمن', nameEn: 'Lower Right Lateral Incisor', jaw: 'lower', quadrant: 4, status: 'healthy', affectedSurfaces: [] },
  { number: 41, nameAr: 'القاطع المركزي السفلي الأيمن', nameEn: 'Lower Right Central Incisor', jaw: 'lower', quadrant: 4, status: 'healthy', affectedSurfaces: [] },
];

export interface DentalJawOdontogram3DProps {
  onSelectTooth?: (tooth: ToothData) => void;
}

export const DentalJawOdontogram3D: React.FC<DentalJawOdontogram3DProps> = ({ onSelectTooth }) => {
  const [teeth, setTeeth] = useState<ToothData[]>(INITIAL_TEETH_CATALOG);
  const [selectedNumber, setSelectedNumber] = useState<number>(36);
  const [jawFilter, setJawFilter] = useState<'both' | 'upper' | 'lower'>('both');
  const [is3DView, setIs3DView] = useState(true);

  const selectedTooth = teeth.find((t) => t.number === selectedNumber) || teeth[0];

  const handleToothClick = (t: ToothData) => {
    setSelectedNumber(t.number);
    onSelectTooth?.(t);
  };

  const handleStatusChange = (newStatus: ToothStatus) => {
    setTeeth((prev) =>
      prev.map((t) => (t.number === selectedNumber ? { ...t, status: newStatus } : t))
    );
  };

  const handleSurfaceToggle = (surface: ToothSurface) => {
    setTeeth((prev) =>
      prev.map((t) => {
        if (t.number !== selectedNumber) return t;
        const exists = t.affectedSurfaces.includes(surface);
        const nextSurfaces = exists
          ? t.affectedSurfaces.filter((s) => s !== surface)
          : [...t.affectedSurfaces, surface];
        return { ...t, affectedSurfaces: nextSurfaces };
      })
    );
  };

  // Color mapping based on status
  const getStatusColor = (status: ToothStatus) => {
    switch (status) {
      case 'caries': return { bg: '#fee2e2', border: '#ef4444', text: '#b91c1c', badge: 'تسوس نشط' };
      case 'rct': return { bg: '#dbeafe', border: '#3b82f6', text: '#1d4ed8', badge: 'علاج عصب' };
      case 'crown': return { bg: '#fef3c7', border: '#f59e0b', text: '#b45309', badge: 'تاج / تركيبة' };
      case 'extracted': return { bg: '#f1f5f9', border: '#94a3b8', text: '#64748b', badge: 'مخلوع' };
      case 'implant': return { bg: '#f3e8ff', border: '#a855f7', text: '#7e22ce', badge: 'زراعة سنية' };
      default: return { bg: 'var(--clinic-bg-surface)', border: 'var(--clinic-border-strong)', text: 'var(--clinic-text-primary)', badge: 'سليم' };
    }
  };

  // Divide into upper and lower arcs
  const upperRightTeeth = teeth.filter((t) => t.quadrant === 1).sort((a, b) => b.number - a.number); // 18 down to 11
  const upperLeftTeeth = teeth.filter((t) => t.quadrant === 2).sort((a, b) => a.number - b.number);  // 21 up to 28
  const lowerRightTeeth = teeth.filter((t) => t.quadrant === 4).sort((a, b) => b.number - a.number); // 48 down to 41
  const lowerLeftTeeth = teeth.filter((t) => t.quadrant === 3).sort((a, b) => a.number - b.number);  // 31 up to 38

  return (
    <div className="clinic-card" style={{ padding: '1.5rem', display: 'grid', gap: '1.25rem' }}>
      {/* Odontogram Header & View Controls */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '1rem' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <h3 style={{ fontSize: '1.125rem', fontWeight: 700, color: 'var(--clinic-text-primary)' }}>
              مخطط الفك والأسنان ثلاثي الأبعاد (3D Dual-Arch Odontogram)
            </h3>
            <span
              style={{
                fontSize: '0.6875rem',
                backgroundColor: 'var(--clinic-accent-soft)',
                color: 'var(--clinic-accent-primary)',
                padding: '0.125rem 0.5rem',
                borderRadius: 'var(--clinic-radius-pill)',
                fontWeight: 700,
              }}
            >
              وحدة نمطية نشطة
            </span>
          </div>
          <p style={{ fontSize: '0.8125rem', color: 'var(--clinic-text-secondary)', marginTop: '0.25rem' }}>
            مجسم تشريحي مقوس تفاعلي للفكين العلوي (Maxilla) والسفلي (Mandible) مع فحص الأسطح الخمسة
          </p>
        </div>

        {/* View Switchers */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <div style={{ display: 'flex', backgroundColor: 'var(--clinic-bg-subtle)', padding: '0.25rem', borderRadius: 'var(--clinic-radius-input)' }}>
            <button
              type="button"
              onClick={() => setJawFilter('both')}
              style={{
                padding: '0.375rem 0.75rem',
                borderRadius: '6px',
                border: 'none',
                background: jawFilter === 'both' ? 'var(--clinic-bg-surface)' : 'transparent',
                fontWeight: jawFilter === 'both' ? 700 : 500,
                color: jawFilter === 'both' ? 'var(--clinic-accent-primary)' : 'var(--clinic-text-secondary)',
                cursor: 'pointer',
                fontSize: '0.75rem',
                boxShadow: jawFilter === 'both' ? 'var(--clinic-shadow-sm)' : 'none',
              }}
            >
              كلا الفكين 3D
            </button>
            <button
              type="button"
              onClick={() => setJawFilter('upper')}
              style={{
                padding: '0.375rem 0.75rem',
                borderRadius: '6px',
                border: 'none',
                background: jawFilter === 'upper' ? 'var(--clinic-bg-surface)' : 'transparent',
                fontWeight: jawFilter === 'upper' ? 700 : 500,
                color: jawFilter === 'upper' ? 'var(--clinic-accent-primary)' : 'var(--clinic-text-secondary)',
                cursor: 'pointer',
                fontSize: '0.75rem',
                boxShadow: jawFilter === 'upper' ? 'var(--clinic-shadow-sm)' : 'none',
              }}
            >
              الفك العلوي
            </button>
            <button
              type="button"
              onClick={() => setJawFilter('lower')}
              style={{
                padding: '0.375rem 0.75rem',
                borderRadius: '6px',
                border: 'none',
                background: jawFilter === 'lower' ? 'var(--clinic-bg-surface)' : 'transparent',
                fontWeight: jawFilter === 'lower' ? 700 : 500,
                color: jawFilter === 'lower' ? 'var(--clinic-accent-primary)' : 'var(--clinic-text-secondary)',
                cursor: 'pointer',
                fontSize: '0.75rem',
                boxShadow: jawFilter === 'lower' ? 'var(--clinic-shadow-sm)' : 'none',
              }}
            >
              الفك السفلي
            </button>
          </div>

          <button
            type="button"
            onClick={() => setIs3DView(!is3DView)}
            style={{
              padding: '0.375rem 0.75rem',
              borderRadius: 'var(--clinic-radius-input)',
              backgroundColor: is3DView ? 'var(--clinic-accent-soft)' : 'var(--clinic-bg-subtle)',
              border: '1px solid var(--clinic-border-subtle)',
              color: is3DView ? 'var(--clinic-accent-primary)' : 'var(--clinic-text-secondary)',
              fontSize: '0.75rem',
              fontWeight: 600,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '0.375rem',
            }}
          >
            <Sparkles size={14} />
            <span>{is3DView ? 'منظور مجسم 3D' : 'منظور مسطح'}</span>
          </button>
        </div>
      </div>

      {/* Main Dental Arch Simulation Canvas */}
      <div
        className="dental-arch-canvas"
        style={{
          perspective: is3DView ? '900px' : 'none',
          display: 'grid',
          gap: '2rem',
          minHeight: '380px',
        }}
      >
        {/* UPPER JAW (Maxilla) */}
        {(jawFilter === 'both' || jawFilter === 'upper') && (
          <div
            className="gum-ridge-upper"
            style={{
              padding: '1.25rem 1rem 1.75rem',
              transform: is3DView ? 'rotateX(12deg)' : 'none',
              transition: 'transform 0.3s ease',
            }}
          >
            <div style={{ textAlign: 'center', marginBottom: '0.75rem' }}>
              <span style={{ fontSize: '0.75rem', fontWeight: 700, color: 'var(--clinic-text-muted)', letterSpacing: '0.05em' }}>
                الفك العلوي — MAXILLA ARCH (18 - 28)
              </span>
            </div>

            <div style={{ display: 'flex', justifyContent: 'center', gap: '0.375rem', flexWrap: 'wrap' }}>
              {/* Right Quadrant 1 */}
              <div style={{ display: 'flex', gap: '0.25rem' }}>
                {upperRightTeeth.map((tooth) => renderToothButton(tooth))}
              </div>

              {/* Midline Divider */}
              <div
                style={{
                  width: '2px',
                  backgroundColor: 'rgba(244, 63, 94, 0.4)',
                  marginInline: '0.375rem',
                  borderRadius: '2px',
                }}
              />

              {/* Left Quadrant 2 */}
              <div style={{ display: 'flex', gap: '0.25rem' }}>
                {upperLeftTeeth.map((tooth) => renderToothButton(tooth))}
              </div>
            </div>
          </div>
        )}

        {/* LOWER JAW (Mandible) */}
        {(jawFilter === 'both' || jawFilter === 'lower') && (
          <div
            className="gum-ridge-lower"
            style={{
              padding: '1.75rem 1rem 1.25rem',
              transform: is3DView ? 'rotateX(-10deg)' : 'none',
              transition: 'transform 0.3s ease',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'center', gap: '0.375rem', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
              {/* Right Quadrant 4 */}
              <div style={{ display: 'flex', gap: '0.25rem' }}>
                {lowerRightTeeth.map((tooth) => renderToothButton(tooth))}
              </div>

              {/* Midline Divider */}
              <div
                style={{
                  width: '2px',
                  backgroundColor: 'rgba(244, 63, 94, 0.4)',
                  marginInline: '0.375rem',
                  borderRadius: '2px',
                }}
              />

              {/* Left Quadrant 3 */}
              <div style={{ display: 'flex', gap: '0.25rem' }}>
                {lowerLeftTeeth.map((tooth) => renderToothButton(tooth))}
              </div>
            </div>

            <div style={{ textAlign: 'center' }}>
              <span style={{ fontSize: '0.75rem', fontWeight: 700, color: 'var(--clinic-text-muted)', letterSpacing: '0.05em' }}>
                الفك السفلي — MANDIBLE ARCH (48 - 38)
              </span>
            </div>
          </div>
        )}
      </div>

      {/* Selected Tooth Deep Inspector Panel */}
      {selectedTooth && (
        <div
          style={{
            padding: '1.25rem',
            backgroundColor: 'var(--clinic-bg-subtle)',
            borderRadius: 'var(--clinic-radius-card)',
            border: '1px solid var(--clinic-border-subtle)',
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
            gap: '1.25rem',
            alignItems: 'center',
          }}
        >
          {/* Tooth Identification */}
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.375rem' }}>
              <span
                style={{
                  fontSize: '1.25rem',
                  fontWeight: 800,
                  color: 'var(--clinic-accent-primary)',
                  backgroundColor: 'var(--clinic-bg-surface)',
                  padding: '0.25rem 0.625rem',
                  borderRadius: 'var(--clinic-radius-input)',
                  border: '1px solid var(--clinic-border-strong)',
                }}
              >
                #{selectedTooth.number}
              </span>
              <div>
                <h4 style={{ fontSize: '0.9375rem', fontWeight: 700, color: 'var(--clinic-text-primary)' }}>
                  {selectedTooth.nameAr}
                </h4>
                <span style={{ fontSize: '0.75rem', color: 'var(--clinic-text-muted)' }}>
                  {selectedTooth.nameEn} ({selectedTooth.jaw === 'upper' ? 'الفك العلوي' : 'الفك السفلي'})
                </span>
              </div>
            </div>

            {selectedTooth.notes && (
              <p style={{ fontSize: '0.8125rem', color: 'var(--clinic-text-secondary)', marginTop: '0.5rem' }}>
                <strong>الملاحظات السريرية:</strong> {selectedTooth.notes}
              </p>
            )}
          </div>

          {/* Clinical Status Selector */}
          <div>
            <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 700, color: 'var(--clinic-text-muted)', marginBottom: '0.375rem' }}>
              الحالة السريرية والإجراء:
            </label>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.375rem' }}>
              {[
                { id: 'healthy', label: 'سليم', color: '#16a34a' },
                { id: 'caries', label: 'تسوس', color: '#dc2626' },
                { id: 'rct', label: 'حشو عصب', color: '#2563eb' },
                { id: 'crown', label: 'تركيبة / تاج', color: '#d97706' },
                { id: 'extracted', label: 'مخلوع', color: '#64748b' },
                { id: 'implant', label: 'زراعة', color: '#9333ea' },
              ].map((st) => (
                <button
                  key={st.id}
                  type="button"
                  onClick={() => handleStatusChange(st.id as ToothStatus)}
                  style={{
                    padding: '0.375rem 0.625rem',
                    borderRadius: 'var(--clinic-radius-input)',
                    fontSize: '0.75rem',
                    fontWeight: 600,
                    border: selectedTooth.status === st.id ? `2px solid ${st.color}` : '1px solid var(--clinic-border-subtle)',
                    backgroundColor: selectedTooth.status === st.id ? 'var(--clinic-bg-surface)' : 'transparent',
                    color: selectedTooth.status === st.id ? st.color : 'var(--clinic-text-secondary)',
                    cursor: 'pointer',
                  }}
                >
                  {st.label}
                </button>
              ))}
            </div>
          </div>

          {/* 5 Anatomical Surfaces Interactive Selector */}
          <div>
            <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 700, color: 'var(--clinic-text-muted)', marginBottom: '0.375rem' }}>
              الأسطح المتأثرة (5 Anatomical Surfaces):
            </label>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.375rem' }}>
              {[
                { id: 'occlusal', label: 'إطباقي (O)' },
                { id: 'mesial', label: 'إنسي (M)' },
                { id: 'distal', label: 'وحشي (D)' },
                { id: 'buccal', label: 'دهليزي (B)' },
                { id: 'lingual', label: 'لساني (L)' },
              ].map((surf) => {
                const isChecked = selectedTooth.affectedSurfaces.includes(surf.id as ToothSurface);
                return (
                  <button
                    key={surf.id}
                    type="button"
                    onClick={() => handleSurfaceToggle(surf.id as ToothSurface)}
                    style={{
                      padding: '0.25rem 0.5rem',
                      borderRadius: 'var(--clinic-radius-pill)',
                      fontSize: '0.6875rem',
                      fontWeight: 600,
                      border: isChecked ? '1px solid var(--clinic-accent-primary)' : '1px solid var(--clinic-border-subtle)',
                      backgroundColor: isChecked ? 'var(--clinic-accent-soft)' : 'var(--clinic-bg-surface)',
                      color: isChecked ? 'var(--clinic-accent-primary)' : 'var(--clinic-text-secondary)',
                      cursor: 'pointer',
                    }}
                  >
                    {surf.label}
                  </button>
                );
              })}
            </div>
          </div>
        </div>
      )}
    </div>
  );

  // Helper renderer for each 3D-styled tooth crown
  function renderToothButton(tooth: ToothData) {
    const isSelected = selectedNumber === tooth.number;
    const style = getStatusColor(tooth.status);

    return (
      <button
        key={tooth.number}
        type="button"
        onClick={() => handleToothClick(tooth)}
        className="tooth-3d-card"
        title={`${tooth.nameAr} (#${tooth.number}) - ${style.badge}`}
        style={{
          width: '38px',
          height: '52px',
          borderRadius: '8px',
          border: isSelected ? '2px solid var(--clinic-accent-primary)' : `1px solid ${style.border}`,
          backgroundColor: isSelected ? 'var(--clinic-accent-soft)' : style.bg,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0.25rem 0.125rem',
          cursor: 'pointer',
          outline: 'none',
          position: 'relative',
        }}
      >
        <span
          style={{
            fontSize: '0.625rem',
            fontWeight: 800,
            color: isSelected ? 'var(--clinic-accent-primary)' : style.text,
            fontFamily: 'Inter, sans-serif',
          }}
        >
          {tooth.number}
        </span>

        {/* 3D Anatomical Crown Cusp Simulation */}
        <div
          style={{
            width: '20px',
            height: '20px',
            borderRadius: tooth.number % 10 <= 3 ? '4px' : '50%',
            backgroundColor: tooth.status === 'extracted' ? '#cbd5e1' : '#ffffff',
            border: `1px solid ${isSelected ? 'var(--clinic-accent-primary)' : '#cbd5e1'}`,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            boxShadow: 'inset 0 1px 2px rgba(0,0,0,0.1)',
            position: 'relative',
          }}
        >
          {tooth.status === 'extracted' ? (
            <span style={{ fontSize: '0.625rem', color: '#ef4444', fontWeight: 900 }}>✕</span>
          ) : tooth.status === 'rct' ? (
            <div style={{ width: '4px', height: '10px', backgroundColor: '#3b82f6', borderRadius: '1px' }} />
          ) : tooth.status === 'crown' ? (
            <div style={{ width: '12px', height: '12px', borderRadius: '2px', backgroundColor: '#f59e0b' }} />
          ) : tooth.status === 'implant' ? (
            <div style={{ width: '6px', height: '12px', borderRadius: '1px', backgroundColor: '#9333ea' }} />
          ) : tooth.affectedSurfaces.length > 0 ? (
            <div style={{ width: '6px', height: '6px', borderRadius: '50%', backgroundColor: '#ef4444' }} />
          ) : (
            <div style={{ width: '6px', height: '6px', borderRadius: '50%', backgroundColor: '#22c55e', opacity: 0.7 }} />
          )}
        </div>

        <span
          style={{
            fontSize: '0.5rem',
            color: 'var(--clinic-text-muted)',
            fontWeight: 600,
            whiteSpace: 'nowrap',
            overflow: 'hidden',
          }}
        >
          {tooth.status === 'caries' ? 'تسوس' : tooth.status === 'rct' ? 'عصب' : tooth.status === 'crown' ? 'تاج' : tooth.status === 'extracted' ? 'مخلوع' : 'سليم'}
        </span>
      </button>
    );
  }
};
