import React from 'react';
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StatCard } from './StatCard';
import { StatusChip } from './StatusChip';
import { Stepper } from './Stepper';
import { ClinicalTable } from './ClinicalTable';
import { ClinicHeader } from './ClinicHeader';
import { AlertBanner } from './AlertBanner';
import { GlassButton } from './GlassButton';

describe('Stitch Design System UI Components', () => {
  it('renders StatCard with formatted value and trend indicator', () => {
    render(
      <StatCard
        title="مواعيد اليوم"
        value="42"
        subtitle="ضمن المعيار الطبي"
        trend={{ value: "+12%", isPositive: true }}
      />
    );

    expect(screen.getByText('مواعيد اليوم')).toBeDefined();
    expect(screen.getByText('42')).toBeDefined();
    expect(screen.getByText('+12%')).toBeDefined();
    expect(screen.getByText('ضمن المعيار الطبي')).toBeDefined();
  });

  it('renders StatusChip with correct status styling', () => {
    const { rerender } = render(<StatusChip status="confirmed" label="مؤكد" />);
    expect(screen.getByText('مؤكد')).toBeDefined();

    rerender(<StatusChip status="cancelled" label="ملغى" />);
    expect(screen.getByText('ملغى')).toBeDefined();

    rerender(<StatusChip status="pending" label="قيد الانتظار" />);
    expect(screen.getByText('قيد الانتظار')).toBeDefined();
  });

  it('renders Stepper with active and upcoming steps', () => {
    const steps = [
      { id: 1, label: 'حجز الموعد' },
      { id: 2, label: 'الفحص السريري' },
      { id: 3, label: 'الوصفة الطبية' },
    ];

    render(<Stepper steps={steps} currentStepIndex={1} />);
    expect(screen.getByText('حجز الموعد')).toBeDefined();
    expect(screen.getByText('الفحص السريري')).toBeDefined();
    expect(screen.getByText('الوصفة الطبية')).toBeDefined();
  });

  it('renders ClinicalTable with rows and empty fallback', () => {
    interface TestItem {
      id: string;
      name: string;
      status: string;
    }

    const columns = [
      { key: 'id', header: 'المعرف' },
      { key: 'name', header: 'الاسم' },
      { key: 'status', header: 'الحالة' },
    ];

    const data: TestItem[] = [
      { id: '1', name: 'أحمد علي', status: 'نشط' },
      { id: '2', name: 'سارة خالد', status: 'مؤكد' },
    ];

    const { rerender } = render(
      <ClinicalTable
        columns={columns}
        data={data}
        keyExtractor={(item) => item.id}
      />
    );

    expect(screen.getByText('أحمد علي')).toBeDefined();
    expect(screen.getByText('سارة خالد')).toBeDefined();

    rerender(
      <ClinicalTable
        columns={columns}
        data={[]}
        keyExtractor={(item) => (item as any).id}
        emptyMessage="لا يوجد مرضى حالياً"
      />
    );

    expect(screen.getByText('لا يوجد مرضى حالياً')).toBeDefined();
  });

  it('renders ClinicHeader with title, badge and controls', () => {
    render(
      <ClinicHeader
        title="منظومة العيادات التخصصية"
        subtitle="بوابة الكادر الطبي الموحدة"
        badge="v1.0"
      />
    );

    expect(screen.getByText('منظومة العيادات التخصصية')).toBeDefined();
    expect(screen.getByText('بوابة الكادر الطبي الموحدة')).toBeDefined();
    expect(screen.getByText('v1.0')).toBeDefined();
  });

  it('renders AlertBanner with mandatory allergy styling', () => {
    render(
      <AlertBanner
        isAllergy={true}
        title="تنبيه الحساسية الطبية"
        message="حساسية مفرطة من مادة البنسلين"
      />
    );

    expect(screen.getByText('تنبيه الحساسية الطبية')).toBeDefined();
    expect(screen.getByText('حساسية مفرطة من مادة البنسلين')).toBeDefined();
  });

  it('renders GlassButton with prominent and soft variants', () => {
    const { rerender } = render(<GlassButton variant="primary">حجز موعد</GlassButton>);
    expect(screen.getByRole('button', { name: 'حجز موعد' })).toBeDefined();

    rerender(<GlassButton variant="soft">تحميل التقرير</GlassButton>);
    expect(screen.getByRole('button', { name: 'تحميل التقرير' })).toBeDefined();

    rerender(<GlassButton variant="danger">إلغاء الموعد</GlassButton>);
    expect(screen.getByRole('button', { name: 'إلغاء الموعد' })).toBeDefined();
  });
});
