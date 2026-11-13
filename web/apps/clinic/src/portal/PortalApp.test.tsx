import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { PortalApp } from './PortalApp';

describe('PortalApp', () => {
  it('renders clinic branding in Arabic RTL and handles login plus allergy banner and signed download', async () => {
    const onDownload = vi.fn().mockResolvedValue('/api/v1/c/smile-dental/portal/downloads/prescription/rx-1?token=abc');
    render(
      <PortalApp
        slug="smile-dental"
        initialOverview={{
          clinicId: 'clinic-1',
          clinicName: 'عيادة بسمة',
          clinicSlug: 'smile-dental',
          welcomeMessageAr: 'أهلاً بكم',
          welcomeMessageEn: 'Welcome',
          availableSections: ['overview', 'medical-record', 'visits', 'treatment-plans'],
          summary: {
            patientId: 'pat-1',
            fileNumber: 'SM-0001',
            fullName: 'نادية حسن',
            bloodType: 'O+',
          },
          medicalBackground: {
            allergies: [{ id: 'al-1', allergen: 'Aspirin', severity: 'Severe' }],
            chronicConditions: ['Asthma'],
            currentMedications: ['Inhaler'],
            pastSurgeries: [],
            visibleNotes: [{ noteId: 'n-1', noteText: 'تجنب المشروبات الباردة', createdAtUtc: '2026-06-08T09:00:00Z' }],
            sharedAttachments: [],
          },
          upcomingAppointments: [],
          visits: [
            {
              visitId: 'v-1',
              startedAtUtc: '2026-06-08T09:00:00Z',
              endedAtUtc: '2026-06-08T09:30:00Z',
              doctorName: 'د. سلمى',
              specialtyCode: 'dental',
              diagnosisSummary: 'التهاب لثة',
              advice: 'غسول فم',
              prescriptions: [
                {
                  id: 'rx-1',
                  items: [{ medicationName: 'Amoxicillin', dosage: '500mg', frequency: 'TID', duration: '5d' }],
                },
              ],
            },
          ],
          treatmentPlans: [
            {
              planId: 'plan-hidden',
              name: 'خطة تقويم',
              mode: 'FixedTotal',
              status: 'Active',
              totalSessionsCount: 4,
              completedSessionsCount: 1,
              completionPercentage: 25,
              showCostToPatient: false,
              totalCostToPatient: null,
              paidAmountToPatient: null,
              remainingBalanceToPatient: null,
              currency: 'EGP',
              sessions: [],
            },
          ],
        }}
        onDownloadSignedResource={onDownload}
      />,
    );

    expect(screen.getByTestId('patient-portal-shell').getAttribute('dir')).toBe('rtl');
    expect(screen.getByTestId('portal-allergy-banner').textContent).toContain('Aspirin');

    fireEvent.click(screen.getByText('الزيارات والروشتات'));
    fireEvent.click(screen.getByText('تنزيل الروشتة PDF'));
    expect(onDownload).toHaveBeenCalledWith('prescription', 'rx-1');

    fireEvent.click(screen.getByText('خطط العلاج'));
    expect(screen.getByTestId('plan-cost-hidden-plan-hidden').textContent).toContain('مخفية');
  });

  it('supports public landing redirect banner and LTR 6-digit code activation form', () => {
    render(
      <PortalApp
        slug="old-smile"
        initialLanding={{
          clinicId: 'clinic-1',
          name: 'عيادة بسمة',
          currentSlug: 'smile-dental',
          requestedSlug: 'old-smile',
          isRedirect: true,
          redirectUrl: '/c/smile-dental',
          welcomeMessageAr: 'أهلاً بكم',
          welcomeMessageEn: 'Welcome',
          termsTextAr: 'شروط',
          termsTextEn: 'Terms',
          availableSections: ['overview'],
        }}
      />,
    );

    expect(screen.getByRole('alert').textContent).toContain('/c/smile-dental');
    fireEvent.click(screen.getByText('تفعيل برمز البريد'));
    expect(screen.getByTestId('portal-code-ltr').getAttribute('dir')).toBe('ltr');
  });
});

