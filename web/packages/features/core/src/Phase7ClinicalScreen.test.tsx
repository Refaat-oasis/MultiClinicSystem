import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import {
  Phase7ClinicalScreen,
  type ClinicalVisitSessionData,
  type ClinicalSpecialtyFormDefinition,
} from './Phase7ClinicalScreen';

describe('Phase7ClinicalScreen', () => {
  it('renders allergy banner, server timer, and blocks unacknowledged allergy-conflicting prescription', () => {
    const onCreateRx = vi.fn();
    render(<Phase7ClinicalScreen clinicId="clinic-1" onCreatePrescription={onCreateRx} />);

    expect(screen.getByTestId('clinical-allergy-banner').textContent).toContain('Penicillin');
    expect(screen.getByTestId('visit-server-timer').textContent).toContain('+5 د إضافي');

    fireEvent.click(screen.getByText('إصدار الروشتة'));
    expect(onCreateRx).not.toHaveBeenCalled();

    fireEvent.click(screen.getByTestId('ack-allergy-checkbox'));
    fireEvent.click(screen.getByText('إصدار الروشتة'));
    expect(onCreateRx).toHaveBeenCalledWith(
      expect.objectContaining({
        drugName: 'Amoxicillin 500mg',
        acknowledgeAllergyWarning: true,
      }),
    );
  });

  it('toggles FDI and Universal dental chart numbering and records tooth state', () => {
    const onRecordTooth = vi.fn();
    render(<Phase7ClinicalScreen clinicId="clinic-1" onRecordToothState={onRecordTooth} />);

    expect(screen.getByTestId('dental-chart-panel')).toBeTruthy();
    fireEvent.click(screen.getByText('نظام Universal'));
    expect(screen.getByTestId('tooth-btn-16').textContent).toContain('3');

    fireEvent.click(screen.getByTestId('tooth-btn-16'));
    fireEvent.click(screen.getByText('تسجيل حالة السن'));
    expect(onRecordTooth).toHaveBeenCalledWith(
      expect.objectContaining({
        toothCode: '3',
        numberingSystem: 'Universal',
      }),
    );
  });

  it('renders JSON Schema specialty form for non-dental clinics and validates numeric range', () => {
    const eyeVisit: ClinicalVisitSessionData = {
      id: 'visit-eye-1',
      clinicId: 'clinic-b',
      patientId: 'pat-2',
      patientName: 'سامي حسن',
      doctorId: 'doc-2',
      doctorName: 'د. رمد',
      specialtyCode: 'ophthalmology',
      startedAtUtc: '2026-06-08T10:00:00Z',
      plannedDurationMinutes: 20,
      elapsedMinutes: 10,
      overtimeMinutes: 0,
      isOvertimeExcused: false,
      status: 'InProgress',
      isVisibleToPatient: true,
      supportsDentalChart: false,
      patientAllergies: [],
      vitals: [],
      diagnoses: [],
      procedures: [],
      prescriptions: [],
      dentalChart: [],
      formSubmissions: [],
    };
    const forms: ClinicalSpecialtyFormDefinition[] = [
      {
        id: 'form-eye',
        specialtyCode: 'ophthalmology',
        titleAr: 'قياس ضغط العين',
        titleEn: 'IOP',
        jsonSchema: '{"type":"object","required":["iop"],"properties":{"iop":{"type":"number","minimum":5,"maximum":60}}}',
      },
    ];
    const onSaveForm = vi.fn();
    render(
      <Phase7ClinicalScreen
        clinicId="clinic-b"
        initialVisit={eyeVisit}
        specialtyForms={forms}
        onSaveSpecialtyForm={onSaveForm}
      />,
    );

    expect(screen.queryByTestId('dental-chart-panel')).toBeNull();
    expect(screen.getByTestId('specialty-form-panel')).toBeTruthy();

    const input = screen.getByLabelText(/iop/);
    fireEvent.change(input, { target: { value: '90' } });
    fireEvent.click(screen.getByText('حفظ بيانات النموذج'));
    expect(onSaveForm).not.toHaveBeenCalled();

    fireEvent.change(input, { target: { value: '18' } });
    fireEvent.click(screen.getByText('حفظ بيانات النموذج'));
    expect(onSaveForm).toHaveBeenCalledWith({
      clinicalFormId: 'form-eye',
      answersJson: '{"iop":18}',
    });
  });
});

