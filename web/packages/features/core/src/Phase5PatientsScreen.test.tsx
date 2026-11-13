import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { Phase5PatientsScreen, type PatientDetailData, type PatientSummaryItem } from './Phase5PatientsScreen';

const SAMPLE_SUMMARIES: PatientSummaryItem[] = [
  {
    id: 'pat-1',
    fileNumber: 'P-000001',
    fullName: 'أحمد إبراهيم مصطفى',
    phone: '01001234567',
    gender: 'Male',
    ageYears: 36,
    primarySpecialtyCode: 'dental',
    allergyCount: 1,
    hasSevereAllergy: true,
  },
];

const SAMPLE_DETAIL: PatientDetailData = {
  id: 'pat-1',
  clinicId: 'clinic-1',
  fileNumber: 'P-000001',
  fullName: 'أحمد إبراهيم مصطفى',
  phone: '01001234567',
  ageYears: 36,
  gender: 'Male',
  isMedicalHistoryEnabled: true,
  isMedicalHistoryReviewed: true,
  medicalHistorySummary: 'Controlled hypertension',
  chronicConditions: ['Hypertension'],
  currentMedications: [],
  pastSurgeries: [],
  allergies: [
    {
      id: 'alg-1',
      patientId: 'pat-1',
      allergen: 'Penicillin',
      severity: 'Severe',
      reaction: 'Rash',
      createdAtUtc: '2026-11-13T10:00:00Z',
    },
  ],
  notes: [],
  isAttachmentsEnabled: true,
  attachments: [],
  duplicateWarnings: [],
};

describe('Phase5PatientsScreen', () => {
  it('displays persistent allergy warning banner when active allergies exist', () => {
    render(
      <Phase5PatientsScreen
        patients={SAMPLE_SUMMARIES}
        selectedPatient={SAMPLE_DETAIL}
        scopingSettings={{ scopePatientsByDoctor: false, scopePatientsBySpecialty: false }}
      />
    );

    expect(screen.getByTestId('persistent-allergy-banner').textContent).toContain('Penicillin');
    expect(screen.getByText('المرفقات والملفات الطبية')).toBeTruthy();
  });

  it('gracefully hides history and attachments sections when modules are disabled', () => {
    render(
      <Phase5PatientsScreen
        patients={SAMPLE_SUMMARIES}
        selectedPatient={{
          ...SAMPLE_DETAIL,
          isMedicalHistoryEnabled: false,
          isAttachmentsEnabled: false,
        }}
        scopingSettings={{ scopePatientsByDoctor: false, scopePatientsBySpecialty: false }}
      />
    );

    expect(screen.queryByTestId('persistent-allergy-banner')).toBeNull();
    expect(screen.queryByText('الحساسية والتاريخ الطبي')).toBeNull();
    expect(screen.queryByText('المرفقات والملفات الطبية')).toBeNull();
  });

  it('invokes create patient and attachment handlers', () => {
    const onCreatePatient = vi.fn();
    const onUploadAttachment = vi.fn();

    render(
      <Phase5PatientsScreen
        patients={SAMPLE_SUMMARIES}
        selectedPatient={SAMPLE_DETAIL}
        duplicateWarnings={[
          {
            patientId: 'pat-1',
            fileNumber: 'P-000001',
            fullName: 'أحمد إبراهيم مصطفى',
            phone: '01001234567',
            warningReason: 'PHONE_MATCH',
            confidencePercent: 85,
          },
        ]}
        scopingSettings={{ scopePatientsByDoctor: false, scopePatientsBySpecialty: false }}
        onCreatePatient={onCreatePatient}
        onUploadAttachment={onUploadAttachment}
      />
    );

    fireEvent.change(screen.getByPlaceholderText('مثال: أحمد إبراهيم مصطفى'), {
      target: { value: 'سارة علي حسن' },
    });
    fireEvent.change(screen.getByPlaceholderText('01001234567'), {
      target: { value: '01112223334' },
    });
    fireEvent.click(screen.getByText('حفظ المريض'));
    expect(onCreatePatient).toHaveBeenCalledWith(
      expect.objectContaining({
        fullName: 'سارة علي حسن',
        phone: '01112223334',
      })
    );

    fireEvent.click(screen.getByText('رفع مرفق'));
    expect(onUploadAttachment).toHaveBeenCalledWith('pat-1', 'Lab', 'report.png', 'image/png', undefined);
  });
});
