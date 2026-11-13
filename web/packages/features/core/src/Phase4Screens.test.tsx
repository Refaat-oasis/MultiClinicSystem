import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import {
  MyFeaturesScreen,
  RolesPermissionsScreen,
  StaffDevicesScreen,
  ClinicStructureScreen,
  OwnerPortfolioAndWidgetsScreen,
  type ClinicModuleItem,
  type ClinicRoleItem,
  type PermissionDefItem,
  type StaffMemberItem,
} from './Phase4Screens';

const SAMPLE_MODULES: ClinicModuleItem[] = [
  {
    moduleKey: 'patients',
    titleAr: 'إدارة المرضى',
    titleEn: 'Patients',
    descriptionAr: 'سجلات المرضى والملفات',
    descriptionEn: 'Patient files',
    enabled: true,
    prerequisites: [],
    missingPrerequisites: [],
    enabledDependents: ['scheduling'],
  },
  {
    moduleKey: 'billing',
    titleAr: 'الفواتير والمدفوعات',
    titleEn: 'Billing',
    descriptionAr: 'الفواتير والإيصالات',
    descriptionEn: 'Billing',
    enabled: false,
    prerequisites: ['patients'],
    missingPrerequisites: [],
    enabledDependents: [],
  },
];

const SAMPLE_CATALOG: PermissionDefItem[] = [
  { key: 'patients.read', moduleKey: 'patients', group: 'Patients', titleAr: 'عرض المرضى', titleEn: 'Read Patients', isReserved: false },
  { key: 'billing.read', moduleKey: 'billing', group: 'Billing', titleAr: 'عرض الفواتير', titleEn: 'Read Invoices', isReserved: false },
  { key: 'clinic.settings.edit', moduleKey: 'core', group: 'Core', titleAr: 'إعدادات العيادة', titleEn: 'Edit Settings', isReserved: true },
];

const SAMPLE_ROLES: ClinicRoleItem[] = [
  {
    roleId: 'role-recep',
    name: 'Receptionist',
    nameAr: 'استقبال',
    nameEn: 'Receptionist',
    isSystemOwner: false,
    isEditable: true,
    assignedUsersCount: 1,
    permissions: ['patients.read', 'billing.read'],
    effectivePermissions: ['patients.read'],
    dormantPermissions: ['billing.read'],
  },
];

const SAMPLE_STAFF: StaffMemberItem[] = [
  {
    userId: 'staff-1',
    fullName: 'منى عادل',
    email: 'mona@clinic.local',
    roleName: 'Receptionist',
    isOwner: false,
    isActive: true,
    effectivePermissions: ['patients.read'],
    dormantPermissions: ['billing.read'],
    overrides: [],
  },
];

describe('Phase 4 Core UI Screens', () => {
  it('renders read-only MyFeaturesScreen showing enabled and disabled module badges', () => {
    render(
      <MyFeaturesScreen
        modules={SAMPLE_MODULES}
        menuItems={[{ itemKey: 'patients.list', moduleKey: 'patients', titleAr: 'المرضى', titleEn: 'Patients', route: '/patients', requiredPermission: 'patients.read', order: 1 }]}
        effectivePermissions={['patients.read']}
      />
    );

    expect(screen.getByText(/الميزات المفعّلة للعيادة/)).toBeDefined();
    expect(screen.getByText('مفعّل')).toBeDefined();
    expect(screen.getByText('غير مفعّل')).toBeDefined();
  });

  it('renders RolesPermissionsScreen with dormant & owner-reserved badges and triggers role clone', () => {
    const onCloneRole = vi.fn();
    render(
      <RolesPermissionsScreen
        roles={SAMPLE_ROLES}
        catalog={SAMPLE_CATALOG}
        staff={SAMPLE_STAFF}
        onCloneRole={onCloneRole}
        onUpdateRolePermissions={vi.fn()}
        onSetUserOverrides={vi.fn()}
      />
    );

    expect(screen.getByText('خاملة (Dormant)')).toBeDefined();
    expect(screen.getByText('للمالك فقط')).toBeDefined();

    fireEvent.change(screen.getByLabelText(/اسم الدور الجديد عند النسخ/), { target: { value: 'Senior Receptionist' } });
    fireEvent.click(screen.getByRole('button', { name: /نسخ الدور المحدد/ }));
    expect(onCloneRole).toHaveBeenCalledWith('role-recep', 'Senior Receptionist');
  });

  it('renders StaffDevicesScreen with pending device approval button', () => {
    const onApproveDevice = vi.fn();
    render(
      <StaffDevicesScreen
        staff={SAMPLE_STAFF}
        roles={SAMPLE_ROLES}
        devices={[{ id: 'd1', userId: 'staff-1', staffName: 'منى عادل', staffEmail: 'mona@clinic.local', deviceId: 'tab-1', deviceName: 'iPad', isApproved: false, isRevoked: false }]}
        onInviteStaff={vi.fn()}
        onApproveDevice={onApproveDevice}
      />
    );

    fireEvent.click(screen.getByRole('button', { name: /اعتماد الجهاز/ }));
    expect(onApproveDevice).toHaveBeenCalledWith('tab-1');
  });

  it('renders ClinicStructureScreen and reserves number block', () => {
    const onReserveBlock = vi.fn();
    render(
      <ClinicStructureScreen
        rooms={[]}
        doctors={[]}
        workingHours={[]}
        daysOff={[]}
        numberBlocks={[]}
        onCreateRoom={vi.fn()}
        onCreateDoctor={vi.fn()}
        onSaveDefaultHours={vi.fn()}
        onCreateDayOff={vi.fn()}
        onReserveBlock={onReserveBlock}
      />
    );

    fireEvent.click(screen.getByRole('button', { name: /حجز 500 رقم إيصال/ }));
    expect(onReserveBlock).toHaveBeenCalledWith('Receipt');
  });

  it('renders OwnerPortfolioAndWidgetsScreen with isolated error tile fallback and totals only', () => {
    render(
      <OwnerPortfolioAndWidgetsScreen
        widgets={[
          { widgetKey: 'core.rooms-count', moduleKey: 'core', titleAr: 'الغرف النشطة', titleEn: 'Rooms', primaryValue: 4, unitLabel: 'count' },
          { widgetKey: 'billing.revenue', moduleKey: 'billing', titleAr: 'ملخص التحصيل', titleEn: 'Revenue', errorMessage: 'الوحدة مغلقة' },
        ]}
        portfolio={{
          ownerId: 'o1',
          totalClinics: 2,
          activeClinics: 2,
          totalStaffAcrossClinics: 8,
          totalDoctorsAcrossClinics: 3,
          clinics: [
            { clinicId: 'c1', name: 'فرع المعادي', slug: 'maadi', status: 'Active', staffCount: 5, roomsCount: 4, doctorsCount: 2, enabledModulesCount: 4 },
          ],
        }}
        auditLogs={[{ id: 'a1', action: 'NumberBlockReserved', entityName: 'NumberBlock', timestampUtc: '2026-11-13T12:00:00Z' }]}
      />
    );

    expect(screen.getByText(/تعذر تحميل المؤشر: الوحدة مغلقة/)).toBeDefined();
    expect(screen.getByText('فرع المعادي')).toBeDefined();
    expect(screen.getByText('NumberBlockReserved')).toBeDefined();
  });
});

