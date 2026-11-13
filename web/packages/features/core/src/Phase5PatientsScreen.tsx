import React, { useState } from 'react';
import { GlassCard, GlassButton, GlassInput, AlertBanner } from '@clinic/ui';
import {
  Users,
  UserPlus,
  Search,
  AlertTriangle,
  HeartPulse,
  FileText,
  Paperclip,
  ShieldAlert,
  Lock,
} from 'lucide-react';

export interface PatientAllergyItem {
  id: string;
  patientId: string;
  allergen: string;
  severity: 'Mild' | 'Moderate' | 'Severe' | string;
  reaction?: string | null;
  createdAtUtc: string;
}

export interface PatientNoteItem {
  id: string;
  patientId: string;
  doctorUserId?: string | null;
  noteText: string;
  isPrivate: boolean;
  createdAtUtc: string;
}

export interface PatientAttachmentItem {
  id: string;
  patientId: string;
  category: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  hasThumbnail: boolean;
  notes?: string | null;
  createdAtUtc: string;
}

export interface PatientDuplicateWarningItem {
  patientId: string;
  fileNumber: string;
  fullName: string;
  phone: string;
  warningReason: string;
  confidencePercent: number;
}

export interface PatientSummaryItem {
  id: string;
  fileNumber: string;
  fullName: string;
  phone: string;
  gender: string;
  dateOfBirth?: string | null;
  ageYears?: number | null;
  assignedDoctorId?: string | null;
  primarySpecialtyCode?: string | null;
  allergyCount: number;
  hasSevereAllergy: boolean;
}

export interface PatientDetailData {
  id: string;
  clinicId: string;
  fileNumber: string;
  fullName: string;
  phone: string;
  secondaryPhone?: string | null;
  dateOfBirth?: string | null;
  approximateAgeYears?: number | null;
  ageYears?: number | null;
  gender: string;
  nationalId?: string | null;
  address?: string | null;
  assignedDoctorId?: string | null;
  primarySpecialtyCode?: string | null;
  isMedicalHistoryEnabled: boolean;
  isMedicalHistoryReviewed: boolean;
  medicalHistorySummary?: string | null;
  chronicConditions: string[];
  currentMedications: string[];
  pastSurgeries: string[];
  allergies: PatientAllergyItem[];
  notes: PatientNoteItem[];
  isAttachmentsEnabled: boolean;
  attachments: PatientAttachmentItem[];
  duplicateWarnings: PatientDuplicateWarningItem[];
}

export interface PatientScopingSettings {
  scopePatientsByDoctor: boolean;
  scopePatientsBySpecialty: boolean;
}

export interface CreatePatientFormPayload {
  fullName: string;
  phone: string;
  secondaryPhone?: string;
  dateOfBirth?: string;
  approximateAgeYears?: number;
  gender: string;
  primarySpecialtyCode?: string;
  confirmDuplicateCreation: boolean;
}

export interface Phase5PatientsScreenProps {
  patients: PatientSummaryItem[];
  selectedPatient: PatientDetailData | null;
  duplicateWarnings?: PatientDuplicateWarningItem[];
  scopingSettings: PatientScopingSettings;
  readOnlyInspect?: boolean;
  onSearch?: (query: string) => void;
  onSelectPatient?: (patientId: string) => void;
  onCheckDuplicates?: (fullName: string, phone: string) => void;
  onCreatePatient?: (payload: CreatePatientFormPayload) => void;
  onAddAllergy?: (patientId: string, allergen: string, severity: string, reaction?: string) => void;
  onUpdateMedicalHistory?: (patientId: string, summary: string, chronicConditions: string[], markReviewed: boolean) => void;
  onAddNote?: (patientId: string, noteText: string, isPrivate: boolean) => void;
  onUploadAttachment?: (patientId: string, category: string, fileName: string, contentType: string, notes?: string) => void;
  onSaveScoping?: (settings: PatientScopingSettings) => void;
}

export const Phase5PatientsScreen: React.FC<Phase5PatientsScreenProps> = ({
  patients,
  selectedPatient,
  duplicateWarnings = [],
  scopingSettings,
  readOnlyInspect = false,
  onSearch,
  onSelectPatient,
  onCheckDuplicates,
  onCreatePatient,
  onAddAllergy,
  onUpdateMedicalHistory,
  onAddNote,
  onUploadAttachment,
  onSaveScoping,
}) => {
  const [searchQuery, setSearchQuery] = useState('');
  const [fullName, setFullName] = useState('');
  const [phone, setPhone] = useState('');
  const [gender, setGender] = useState('Male');
  const [approxAge, setApproxAge] = useState('30');
  const [specialtyCode, setSpecialtyCode] = useState('general_practice');
  const [confirmDup, setConfirmDup] = useState(false);

  const [allergen, setAllergen] = useState('');
  const [severity, setSeverity] = useState('Severe');
  const [reaction, setReaction] = useState('');

  const [historySummary, setHistorySummary] = useState(selectedPatient?.medicalHistorySummary ?? '');
  const [chronicText, setChronicText] = useState((selectedPatient?.chronicConditions ?? []).join(', '));
  const [markReviewed, setMarkReviewed] = useState(true);

  const [noteText, setNoteText] = useState('');
  const [isPrivateNote, setIsPrivateNote] = useState(false);

  const [attachCategory, setAttachCategory] = useState('Lab');
  const [attachFileName, setAttachFileName] = useState('report.png');
  const [attachNotes, setAttachNotes] = useState('');

  const activeAllergies = selectedPatient?.allergies ?? [];
  const hasSevereAllergy = activeAllergies.some((a) => a.severity === 'Severe');

  return (
    <div dir="rtl" style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      {readOnlyInspect && (
        <AlertBanner
          variant="info"
          title="وضع الفحص للقراءة فقط (Super Admin Inspect Mode)"
          message="التعديل والإضافة معطلان تماماً أثناء فحص ملف المريض."
        />
      )}

      {selectedPatient && selectedPatient.isMedicalHistoryEnabled && activeAllergies.length > 0 && (
        <div role="alert" data-testid="persistent-allergy-banner">
          <AlertBanner
            variant={hasSevereAllergy ? 'error' : 'warning'}
            title={`تنبيه حساسية نشط (${activeAllergies.length})`}
            message={activeAllergies.map((a) => `${a.allergen} (${a.severity})`).join(' — ')}
          />
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(280px, 1fr) minmax(360px, 1.4fr)', gap: '1.25rem' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <GlassCard>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <Users size={20} color="var(--color-accent-teal)" />
              <h2 style={{ fontSize: '1.0625rem', fontWeight: 700 }}>سجل المرضى والبحث العربي</h2>
            </div>

            <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <div style={{ flex: 1 }}>
                <GlassInput
                  label="بحث بالاسم العربي أو الهاتف أو رقم الملف"
                  value={searchQuery}
                  onChange={(e) => {
                    const next = e.target.value;
                    setSearchQuery(next);
                    onSearch?.(next);
                  }}
                  placeholder="أحمد / احمد / 0100..."
                />
              </div>
              <GlassButton
                variant="regular"
                onClick={() => onSearch?.(searchQuery)}
                style={{ alignSelf: 'flex-end' }}
              >
                <Search size={16} />
              </GlassButton>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
              {patients.map((p) => (
                <button
                  key={p.id}
                  type="button"
                  onClick={() => onSelectPatient?.(p.id)}
                  style={{
                    textAlign: 'start',
                    padding: '0.75rem',
                    borderRadius: '0.5rem',
                    border:
                      selectedPatient?.id === p.id
                        ? '1px solid var(--color-accent-teal)'
                        : '1px solid var(--color-border-subtle)',
                    backgroundColor: 'var(--color-surface-elevated)',
                    cursor: 'pointer',
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <strong>{p.fullName}</strong>
                    <span dir="ltr" style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)' }}>
                      {p.fileNumber}
                    </span>
                  </div>
                  <div style={{ fontSize: '0.8125rem', color: 'var(--color-text-secondary)', marginTop: '0.25rem' }}>
                    <span dir="ltr">{p.phone}</span> • {p.ageYears ?? '-'} سنة
                    {p.allergyCount > 0 && (
                      <span style={{ marginInlineStart: '0.5rem', color: 'var(--color-danger, #dc2626)' }}>
                        • حساسية ({p.allergyCount})
                      </span>
                    )}
                  </div>
                </button>
              ))}
              {patients.length === 0 && (
                <p style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)' }}>لا يوجد مرضى مطابقون.</p>
              )}
            </div>
          </GlassCard>

          {!readOnlyInspect && (
            <GlassCard>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
                <UserPlus size={20} color="var(--color-accent-teal)" />
                <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>تسجيل مريض جديد</h3>
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.625rem' }}>
                <GlassInput
                  label="الاسم الكامل"
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  placeholder="مثال: أحمد إبراهيم مصطفى"
                />
                <GlassInput
                  label="رقم الهاتف الأساسي"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  placeholder="01001234567"
                  dir="ltr"
                />
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.5rem' }}>
                  <GlassInput
                    label="العمر التقديري"
                    value={approxAge}
                    onChange={(e) => setApproxAge(e.target.value)}
                  />
                  <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.8125rem' }}>
                    <span>النوع</span>
                    <select
                      value={gender}
                      onChange={(e) => setGender(e.target.value)}
                      style={{
                        padding: '0.5rem',
                        borderRadius: '0.5rem',
                        border: '1px solid var(--color-border-subtle)',
                        backgroundColor: 'var(--color-surface-elevated)',
                      }}
                    >
                      <option value="Male">ذكر (Male)</option>
                      <option value="Female">أنثى (Female)</option>
                      <option value="Unspecified">غير محدد</option>
                    </select>
                  </label>
                </div>

                <GlassInput
                  label="التخصص الأساسي"
                  value={specialtyCode}
                  onChange={(e) => setSpecialtyCode(e.target.value)}
                />

                {duplicateWarnings.length > 0 && (
                  <AlertBanner
                    variant="warning"
                    title="تحذير تكرار محتمل"
                    message={duplicateWarnings
                      .map((w) => `${w.fullName} (${w.fileNumber}) - ${w.confidencePercent}%`)
                      .join(' | ')}
                  />
                )}

                <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.8125rem' }}>
                  <input
                    type="checkbox"
                    checked={confirmDup}
                    onChange={(e) => setConfirmDup(e.target.checked)}
                  />
                  <span>تأكيد الإنشاء رغم تحذير التكرار</span>
                </label>

                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <GlassButton
                    variant="regular"
                    onClick={() => onCheckDuplicates?.(fullName, phone)}
                  >
                    فحص التكرار
                  </GlassButton>
                  <GlassButton
                    variant="prominent"
                    onClick={() => {
                      if (!fullName.trim() || !phone.trim()) return;
                      onCreatePatient?.({
                        fullName: fullName.trim(),
                        phone: phone.trim(),
                        gender,
                        approximateAgeYears: Number(approxAge) || undefined,
                        primarySpecialtyCode: specialtyCode.trim() || undefined,
                        confirmDuplicateCreation: confirmDup,
                      });
                    }}
                  >
                    حفظ المريض
                  </GlassButton>
                </div>
              </div>
            </GlassCard>
          )}

          <GlassCard>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem' }}>
              <ShieldAlert size={18} color="var(--color-accent-teal)" />
              <h3 style={{ fontSize: '0.9375rem', fontWeight: 700 }}>نطاق عرض المرضى بالعيادة</h3>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', fontSize: '0.875rem' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <input
                  type="checkbox"
                  disabled={readOnlyInspect}
                  checked={scopingSettings.scopePatientsByDoctor}
                  onChange={(e) =>
                    onSaveScoping?.({ ...scopingSettings, scopePatientsByDoctor: e.target.checked })
                  }
                />
                <span>حصر رؤية الطبيب على مرضاه فقط</span>
              </label>
              <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <input
                  type="checkbox"
                  disabled={readOnlyInspect}
                  checked={scopingSettings.scopePatientsBySpecialty}
                  onChange={(e) =>
                    onSaveScoping?.({ ...scopingSettings, scopePatientsBySpecialty: e.target.checked })
                  }
                />
                <span>حصر الرؤية حسب التخصص</span>
              </label>
            </div>
          </GlassCard>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          {!selectedPatient ? (
            <GlassCard>
              <p style={{ fontSize: '0.9375rem', color: 'var(--color-text-secondary)' }}>
                اختر مريضاً من القائمة لعرض الملف الطبي والحساسية والمرفقات.
              </p>
            </GlassCard>
          ) : (
            <>
              <GlassCard>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div>
                    <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>{selectedPatient.fullName}</h2>
                    <div style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)' }}>
                      رقم الملف: <strong dir="ltr">{selectedPatient.fileNumber}</strong> • الهاتف:{' '}
                      <span dir="ltr">{selectedPatient.phone}</span> • العمر: {selectedPatient.ageYears ?? '-'} سنة
                    </div>
                  </div>
                  {selectedPatient.isMedicalHistoryEnabled && (
                    <span style={{ fontSize: '0.8125rem', fontWeight: 600 }}>
                      {selectedPatient.isMedicalHistoryReviewed ? '✓ تم مراجعة التاريخ الطبي' : 'بانتظار المراجعة'}
                    </span>
                  )}
                </div>
              </GlassCard>

              {selectedPatient.isMedicalHistoryEnabled && (
                <>
                  <GlassCard>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
                      <AlertTriangle size={18} color="var(--color-accent-teal)" />
                      <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>الحساسية والتاريخ الطبي</h3>
                    </div>

                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', marginBottom: '0.75rem' }}>
                      {selectedPatient.allergies.map((a) => (
                        <div
                          key={a.id}
                          style={{
                            padding: '0.5rem 0.75rem',
                            borderRadius: '0.5rem',
                            border: '1px solid var(--color-border-subtle)',
                            display: 'flex',
                            justifyContent: 'space-between',
                          }}
                        >
                          <span>
                            <strong>{a.allergen}</strong> ({a.severity})
                          </span>
                          <span style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)' }}>
                            {a.reaction ?? ''}
                          </span>
                        </div>
                      ))}
                    </div>

                    {!readOnlyInspect && (
                      <div style={{ display: 'grid', gridTemplateColumns: '1fr auto auto auto', gap: '0.5rem', marginBottom: '1rem' }}>
                        <GlassInput
                          label="المادة المسببة للحساسية"
                          value={allergen}
                          onChange={(e) => setAllergen(e.target.value)}
                          placeholder="Penicillin"
                        />
                        <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.8125rem' }}>
                          <span>الشدة</span>
                          <select
                            value={severity}
                            onChange={(e) => setSeverity(e.target.value)}
                            style={{ padding: '0.5rem', borderRadius: '0.5rem' }}
                          >
                            <option value="Mild">Mild</option>
                            <option value="Moderate">Moderate</option>
                            <option value="Severe">Severe</option>
                          </select>
                        </label>
                        <GlassInput
                          label="التفاعل"
                          value={reaction}
                          onChange={(e) => setReaction(e.target.value)}
                          placeholder="طفح جلدي"
                        />
                        <GlassButton
                          variant="regular"
                          style={{ alignSelf: 'flex-end' }}
                          onClick={() => {
                            if (!allergen.trim()) return;
                            onAddAllergy?.(selectedPatient.id, allergen.trim(), severity, reaction.trim() || undefined);
                            setAllergen('');
                            setReaction('');
                          }}
                        >
                          إضافة حساسية
                        </GlassButton>
                      </div>
                    )}

                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', borderTop: '1px solid var(--color-border-subtle)', paddingTop: '0.75rem' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', fontWeight: 600 }}>
                        <HeartPulse size={16} /> <span>ملخص التاريخ المرضي والأمراض المزمنة</span>
                      </div>
                      <GlassInput
                        label="الملخص الطبي"
                        value={historySummary}
                        disabled={readOnlyInspect}
                        onChange={(e) => setHistorySummary(e.target.value)}
                      />
                      <GlassInput
                        label="الأمراض المزمنة (مفصولة بفاصلة)"
                        value={chronicText}
                        disabled={readOnlyInspect}
                        onChange={(e) => setChronicText(e.target.value)}
                      />
                      {!readOnlyInspect && (
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                          <label style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.8125rem' }}>
                            <input
                              type="checkbox"
                              checked={markReviewed}
                              onChange={(e) => setMarkReviewed(e.target.checked)}
                            />
                            <span>اعتماد مراجعة التاريخ الطبي</span>
                          </label>
                          <GlassButton
                            variant="prominent"
                            onClick={() => {
                              const conditions = chronicText
                                .split(',')
                                .map((s) => s.trim())
                                .filter(Boolean);
                              onUpdateMedicalHistory?.(selectedPatient.id, historySummary, conditions, markReviewed);
                            }}
                          >
                            تحديث التاريخ الطبي
                          </GlassButton>
                        </div>
                      )}
                    </div>
                  </GlassCard>

                  <GlassCard>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
                      <FileText size={18} color="var(--color-accent-teal)" />
                      <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>الملاحظات السريرية والخاصة</h3>
                    </div>

                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', marginBottom: '0.75rem' }}>
                      {selectedPatient.notes.map((n) => (
                        <div
                          key={n.id}
                          style={{
                            padding: '0.5rem 0.75rem',
                            borderRadius: '0.5rem',
                            border: '1px solid var(--color-border-subtle)',
                            display: 'flex',
                            justifyContent: 'space-between',
                          }}
                        >
                          <span>{n.noteText}</span>
                          {n.isPrivate && (
                            <span style={{ display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.75rem' }}>
                              <Lock size={12} /> ملاحظة خاصة
                            </span>
                          )}
                        </div>
                      ))}
                    </div>

                    {!readOnlyInspect && (
                      <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                        <GlassInput
                          label="إضافة ملاحظة"
                          value={noteText}
                          onChange={(e) => setNoteText(e.target.value)}
                          placeholder="اكتب ملاحظة للمريض..."
                        />
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                          <label style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.8125rem' }}>
                            <input
                              type="checkbox"
                              checked={isPrivateNote}
                              onChange={(e) => setIsPrivateNote(e.target.checked)}
                            />
                            <span>ملاحظة خاصة بالطبيب فقط</span>
                          </label>
                          <GlassButton
                            variant="regular"
                            onClick={() => {
                              if (!noteText.trim()) return;
                              onAddNote?.(selectedPatient.id, noteText.trim(), isPrivateNote);
                              setNoteText('');
                            }}
                          >
                            إضافة ملاحظة
                          </GlassButton>
                        </div>
                      </div>
                    )}
                  </GlassCard>
                </>
              )}

              {selectedPatient.isAttachmentsEnabled && (
                <GlassCard>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
                    <Paperclip size={18} color="var(--color-accent-teal)" />
                    <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>المرفقات والملفات الطبية</h3>
                  </div>

                  <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', marginBottom: '0.75rem' }}>
                    {selectedPatient.attachments.map((att) => (
                      <div
                        key={att.id}
                        style={{
                          padding: '0.5rem 0.75rem',
                          borderRadius: '0.5rem',
                          border: '1px solid var(--color-border-subtle)',
                          display: 'flex',
                          justifyContent: 'space-between',
                        }}
                      >
                        <span>
                          <strong>{att.fileName}</strong> ({att.category})
                        </span>
                        <span dir="ltr" style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)' }}>
                          {att.contentType} • {Math.max(1, Math.round(att.sizeBytes / 1024))} KB
                        </span>
                      </div>
                    ))}
                    {selectedPatient.attachments.length === 0 && (
                      <p style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)' }}>لا توجد مرفقات مسجلة.</p>
                    )}
                  </div>

                  {!readOnlyInspect && (
                    <div style={{ display: 'grid', gridTemplateColumns: 'auto 1fr 1fr auto', gap: '0.5rem' }}>
                      <GlassInput label="التصنيف" value={attachCategory} onChange={(e) => setAttachCategory(e.target.value)} />
                      <GlassInput label="اسم الملف" value={attachFileName} onChange={(e) => setAttachFileName(e.target.value)} />
                      <GlassInput label="ملاحظات" value={attachNotes} onChange={(e) => setAttachNotes(e.target.value)} />
                      <GlassButton
                        variant="regular"
                        style={{ alignSelf: 'flex-end' }}
                        onClick={() => {
                          if (!attachFileName.trim()) return;
                          onUploadAttachment?.(
                            selectedPatient.id,
                            attachCategory.trim() || 'Document',
                            attachFileName.trim(),
                            'image/png',
                            attachNotes.trim() || undefined
                          );
                          setAttachNotes('');
                        }}
                      >
                        رفع مرفق
                      </GlassButton>
                    </div>
                  )}
                </GlassCard>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
};

