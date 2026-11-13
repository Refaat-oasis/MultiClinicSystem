using BuildingBlocks.Domain;
using Core.Application;

namespace Core.Infrastructure;

/// <summary>
/// Registry of all core and switchable platform modules (16 modules from PLAN section 2A),
/// enforcing module dependency graphs, permission catalogs, menu manifests, and widget metadata.
/// </summary>
public sealed class ModuleCatalogService : IModuleCatalogService
{
    private static readonly IReadOnlyList<IModuleManifest> Manifests = BuildManifests();
    private static readonly Dictionary<string, IModuleManifest> ManifestByKey =
        Manifests.ToDictionary(m => m.ModuleKey, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ModuleDefinitionResult> GetSwitchableModules()
    {
        return Manifests
            .Where(m => !string.Equals(m.ModuleKey, "core", StringComparison.OrdinalIgnoreCase))
            .Select(m => new ModuleDefinitionResult(
                m.ModuleKey,
                m.TitleAr,
                m.TitleEn,
                m.DescriptionAr,
                m.DescriptionEn,
                m.Prerequisites))
            .ToList();
    }

    public IReadOnlyList<IModuleManifest> GetAllManifests() => Manifests;

    public IReadOnlyList<PermissionDefinitionResult> GetPermissionCatalog()
    {
        return Manifests
            .SelectMany(m => m.Permissions)
            .ToList();
    }

    public IReadOnlyList<DashboardWidgetDefinitionResult> GetAllWidgetDefinitions()
    {
        return Manifests
            .SelectMany(m => m.DashboardWidgets)
            .ToList();
    }

    public IReadOnlyList<ModuleMenuItemResult> GetMenuItemsForModules(
        IEnumerable<string> enabledModules,
        IReadOnlySet<string>? effectivePermissions = null)
    {
        var enabledSet = new HashSet<string>(enabledModules, StringComparer.OrdinalIgnoreCase) { "core" };

        return Manifests
            .Where(m => enabledSet.Contains(m.ModuleKey))
            .SelectMany(m => m.MenuItems)
            .Where(item => effectivePermissions == null || effectivePermissions.Contains(item.RequiredPermission))
            .OrderBy(item => item.Order)
            .ToList();
    }

    public IReadOnlySet<string> GetActivePermissionsForModules(IEnumerable<string> enabledModules)
    {
        var enabledSet = new HashSet<string>(enabledModules, StringComparer.OrdinalIgnoreCase) { "core" };

        return Manifests
            .Where(m => enabledSet.Contains(m.ModuleKey))
            .SelectMany(m => m.Permissions)
            .Select(p => p.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetDirectDependents(string moduleKey)
    {
        if (string.IsNullOrWhiteSpace(moduleKey))
        {
            return Array.Empty<string>();
        }

        string normalized = moduleKey.Trim().ToLowerInvariant();
        return Manifests
            .Where(m => m.Prerequisites.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            .Select(m => m.ModuleKey)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool IsValidSwitchableModule(string moduleKey)
    {
        if (string.IsNullOrWhiteSpace(moduleKey))
        {
            return false;
        }

        string normalized = moduleKey.Trim().ToLowerInvariant();
        return !string.Equals(normalized, "core", StringComparison.OrdinalIgnoreCase) &&
               ManifestByKey.ContainsKey(normalized);
    }

    public Result ValidateModuleChanges(
        IReadOnlySet<string> currentlyEnabledModules,
        IReadOnlyList<ModuleToggleInput> requestedChanges)
    {
        if (requestedChanges == null || requestedChanges.Count == 0)
        {
            return Result.Failure(Error.Validation("MODULES_INPUT_REQUIRED", "At least one module toggle is required."));
        }

        var nextState = new HashSet<string>(currentlyEnabledModules, StringComparer.OrdinalIgnoreCase);

        foreach (var change in requestedChanges)
        {
            if (string.IsNullOrWhiteSpace(change.ModuleKey) || !IsValidSwitchableModule(change.ModuleKey))
            {
                return Result.Failure(Error.Validation("UNKNOWN_MODULE", $"Unknown switchable module '{change.ModuleKey}'."));
            }

            string normalized = change.ModuleKey.Trim().ToLowerInvariant();
            if (change.Enabled)
            {
                nextState.Add(normalized);
            }
            else
            {
                nextState.Remove(normalized);
            }
        }

        // Check disabled modules first so disabling a prerequisite reports MODULE_HAS_DEPENDENTS
        foreach (var change in requestedChanges.Where(c => !c.Enabled))
        {
            string normalized = change.ModuleKey.Trim().ToLowerInvariant();
            var activeDependents = GetDirectDependents(normalized)
                .Where(dep => nextState.Contains(dep))
                .ToList();

            if (activeDependents.Count > 0)
            {
                return Result.Failure(Error.Conflict(
                    "MODULE_HAS_DEPENDENTS",
                    $"Cannot disable module '{normalized}' while dependent module(s) [{string.Join(", ", activeDependents)}] are enabled."));
            }
        }

        // Verify all enabled modules in target state have all prerequisites satisfied
        foreach (string moduleKey in nextState)
        {
            if (!ManifestByKey.TryGetValue(moduleKey, out var manifest))
            {
                continue;
            }

            var missingPrereqs = manifest.Prerequisites
                .Where(p => !nextState.Contains(p))
                .ToList();

            if (missingPrereqs.Count > 0)
            {
                return Result.Failure(Error.Conflict(
                    "MODULE_PREREQUISITE_OFF",
                    $"Cannot enable module '{moduleKey}' because prerequisite(s) [{string.Join(", ", missingPrereqs)}] are disabled."));
            }
        }

        return Result.Success();
    }

    private static IReadOnlyList<IModuleManifest> BuildManifests()
    {
        return
        [
            new ModuleManifestEntry(
                ModuleKey: "core",
                TitleAr: "النواة الأساسية",
                TitleEn: "Core Platform",
                DescriptionAr: "إدارة الحسابات، الأدوار، الصلاحيات، الفروع وهيكل العيادة.",
                DescriptionEn: "Accounts, roles, permissions, clinic settings, and physical structure.",
                Prerequisites: Array.Empty<string>(),
                Permissions:
                [
                    new("clinic.settings.edit", "core", "Clinic Settings", "تعديل إعدادات العيادة", "Edit Clinic Settings", true),
                    new("roles.manage", "core", "Security & Roles", "إدارة الأدوار والصلاحيات", "Manage Roles & Claims", true),
                    new("users.manage", "core", "Security & Roles", "إدارة الموظفين والأجهزة", "Manage Staff & Devices", false),
                    new("structure.manage", "core", "Clinic Structure", "إدارة الغرف والأطباء والمواعيد", "Manage Rooms, Doctors & Hours", false),
                    new("numberblocks.manage", "core", "Clinic Structure", "حجز نطاقات الترقيم", "Reserve Number Blocks", false),
                    new("audit.read", "core", "Security & Roles", "عرض سجل التدقيق", "View Clinic Audit Log", false),
                    new("dashboard.view", "core", "Dashboards", "عرض لوحة التحكم", "View Clinic Dashboard", false),
                    new("portfolio.view", "core", "Dashboards", "عرض محفظة العيادات", "View Owner Portfolio", false)
                ],
                MenuItems:
                [
                    new("core.dashboard", "core", "لوحة التحكم", "Dashboard", "/dashboard", "dashboard.view", 10),
                    new("core.features", "core", "الميزات المفعلة", "My Features", "/features", "dashboard.view", 20),
                    new("core.structure", "core", "هيكل العيادة", "Clinic Structure", "/structure", "structure.manage", 30),
                    new("core.roles", "core", "الأدوار والصلاحيات", "Roles & Claims", "/roles", "roles.manage", 40),
                    new("core.staff", "core", "الموظفون والأجهزة", "Staff & Devices", "/staff", "users.manage", 50),
                    new("core.audit", "core", "سجل التدقيق", "Audit Log", "/audit", "audit.read", 60),
                    new("core.portfolio", "core", "محفظة المالك", "Owner Portfolio", "/portfolio", "portfolio.view", 70)
                ],
                SettingsSections:
                [
                    new("general", "البيانات العامة للعيادة", "General Clinic Information", "clinic.settings.edit", null)
                ],
                DashboardWidgets:
                [
                    new("core.staff-count", "core", "dashboard.view", "Clinic", "إجمالي الطاقم", "Active Staff Members"),
                    new("core.roles-count", "core", "dashboard.view", "Clinic", "الأدوار النشطة", "Active Roles"),
                    new("core.devices-pending", "core", "users.manage", "Clinic", "أجهزة بانتظار الاعتماد", "Pending Device Approvals"),
                    new("core.rooms-count", "core", "dashboard.view", "Clinic", "الغرف النشطة", "Active Clinic Rooms"),
                    new("core.doctors-count", "core", "dashboard.view", "Clinic", "الأطباء المسجلون", "Registered Doctors")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "patients",
                TitleAr: "إدارة المرضى",
                TitleEn: "Patients Registry",
                DescriptionAr: "سجلات المرضى والبحث الذكي والدمج.",
                DescriptionEn: "Patient profiles, Arabic search, and file numbering.",
                Prerequisites: Array.Empty<string>(),
                Permissions:
                [
                    new("patients.read", "patients", "Patients", "عرض بيانات المرضى", "Read Patients", false),
                    new("patients.write", "patients", "Patients", "إضافة وتعديل المرضى", "Create & Edit Patients", false)
                ],
                MenuItems:
                [
                    new("patients.list", "patients", "المرضى", "Patients", "/patients", "patients.read", 110)
                ],
                SettingsSections:
                [
                    new("patients.scoping", "نطاق عرض المرضى", "Patient Scoping", "patients.write", "patients")
                ],
                DashboardWidgets:
                [
                    new("patients.summary", "patients", "patients.read", "Clinic", "ملخص المرضى", "Patients Summary")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "patient-history",
                TitleAr: "التاريخ الطبي والحساسية",
                TitleEn: "Medical History & Allergies",
                DescriptionAr: "الأمراض المزمنة والتنبيهات الطبية والحساسية.",
                DescriptionEn: "Allergies, chronic conditions, and medical warnings.",
                Prerequisites: ["patients"],
                Permissions:
                [
                    new("patients.history.read", "patient-history", "Medical History", "عرض التاريخ الطبي", "Read Medical History", false),
                    new("patients.history.write", "patient-history", "Medical History", "تحديث التاريخ الطبي", "Update Medical History", false)
                ],
                MenuItems:
                [
                    new("patient-history.alerts", "patient-history", "تنبيهات الحساسية", "Allergy Alerts", "/patients/history", "patients.history.read", 120)
                ],
                SettingsSections: Array.Empty<SettingsSectionResult>(),
                DashboardWidgets:
                [
                    new("patient-history.allergy-count", "patient-history", "patients.history.read", "Clinic", "تنبيهات الحساسية", "Active Allergies")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "attachments",
                TitleAr: "المرفقات والصور",
                TitleEn: "Attachments & Files",
                DescriptionAr: "رفع الصور والتقارير والمستندات الطبية.",
                DescriptionEn: "Images, scans, lab reports, and document attachments.",
                Prerequisites: ["patients"],
                Permissions:
                [
                    new("attachments.read", "attachments", "Attachments", "عرض المرفقات", "View Attachments", false),
                    new("attachments.write", "attachments", "Attachments", "رفع وحذف المرفقات", "Upload Attachments", false)
                ],
                MenuItems:
                [
                    new("attachments.library", "attachments", "المرفقات", "Attachments", "/attachments", "attachments.read", 130)
                ],
                SettingsSections: Array.Empty<SettingsSectionResult>(),
                DashboardWidgets:
                [
                    new("attachments.uploads-summary", "attachments", "attachments.read", "Clinic", "ملخص المرفقات", "Attachments Summary")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "scheduling",
                TitleAr: "المواعيد والجدولة",
                TitleEn: "Scheduling & Appointments",
                DescriptionAr: "تقويم العيادة وحجز المواعيد ومنع التعارض.",
                DescriptionEn: "Appointments calendar, slots, and conflict prevention.",
                Prerequisites: ["patients"],
                Permissions:
                [
                    new("scheduling.read", "scheduling", "Scheduling", "عرض المواعيد", "View Appointments", false),
                    new("scheduling.write", "scheduling", "Scheduling", "إدارة وحجز المواعيد", "Book & Manage Appointments", false),
                    new("scheduling.priority", "scheduling", "Scheduling", "تحديد أولوية الحجز (VIP / عاجل)", "Assign VIP & Rush Priority", false)
                ],
                MenuItems:
                [
                    new("scheduling.calendar", "scheduling", "المواعيد", "Appointments", "/appointments", "scheduling.read", 140)
                ],
                SettingsSections:
                [
                    new("scheduling-settings", "إعدادات المواعيد", "Scheduling Settings", "clinic.settings.edit", "scheduling")
                ],
                DashboardWidgets:
                [
                    new("scheduling.appointments-summary", "scheduling", "scheduling.read", "Clinic", "ملخص المواعيد", "Appointments Summary"),
                    new("scheduling.utilization", "scheduling", "scheduling.read", "Clinic", "معدل إشغال الأطباء والغرف", "Doctor & Room Utilization")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "queue",
                TitleAr: "طابور الانتظار",
                TitleEn: "Waiting Queue",
                DescriptionAr: "شاشة الانتظار وترتيب الدخول الفوري.",
                DescriptionEn: "Live queue ordering and check-in workflow.",
                Prerequisites: ["scheduling"],
                Permissions:
                [
                    new("queue.read", "queue", "Queue", "عرض طابور الانتظار", "View Queue", false),
                    new("queue.manage", "queue", "Queue", "إدارة النداء والانتظار", "Manage Queue Flow", false)
                ],
                MenuItems:
                [
                    new("queue.board", "queue", "طابور الانتظار", "Queue Board", "/queue", "queue.read", 150)
                ],
                SettingsSections: Array.Empty<SettingsSectionResult>(),
                DashboardWidgets:
                [
                    new("queue.waiting-count", "queue", "queue.read", "Clinic", "المنتظرون حالياً", "Waiting Patients"),
                    new("queue.waiting-list", "queue", "queue.read", "Clinic", "قائمة الانتظار وعروض المواعيد", "Waiting List & Offers")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "clinical",
                TitleAr: "الكشف والجلسات السريرية",
                TitleEn: "Clinical Sessions",
                DescriptionAr: "توثيق الكشف والروشتة ومخطط الأسنان.",
                DescriptionEn: "Clinical visits, prescriptions, and dental charting.",
                Prerequisites: ["patients", "scheduling"],
                Permissions:
                [
                    new("clinical.read", "clinical", "Clinical", "عرض الجلسات السريرية", "View Clinical Sessions", false),
                    new("clinical.write", "clinical", "Clinical", "تدوين الكشف والروشتة", "Record Clinical Visits", false)
                ],
                MenuItems:
                [
                    new("clinical.sessions", "clinical", "الجلسات السريرية", "Clinical Visits", "/clinical", "clinical.read", 160)
                ],
                SettingsSections: Array.Empty<SettingsSectionResult>(),
                DashboardWidgets:
                [
                    new("clinical.sessions-summary", "clinical", "clinical.read", "Doctor", "الجلسات الطبية", "Clinical Sessions Summary"),
                    new("clinical.top-diagnoses", "clinical", "clinical.read", "Doctor", "أبرز التشخيصات والإجراءات", "Top Diagnoses & Procedures"),
                    new("clinical.avg-duration", "clinical", "clinical.read", "Doctor", "متوسط مدة الجلسة", "Average Session Length"),
                    new("clinical.overtime-sessions", "clinical", "clinical.read", "Doctor", "الجلسات المتجاوزة للوقت", "Overtime Sessions")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "billing",
                TitleAr: "الفواتير والمدفوعات",
                TitleEn: "Billing & Invoices",
                DescriptionAr: "الفواتير، الإيصالات، الخصومات، والمرتجعات.",
                DescriptionEn: "Invoices, receipts, discounts, and refunds.",
                Prerequisites: ["patients"],
                Permissions:
                [
                    new("billing.read", "billing", "Billing", "عرض الفواتير", "View Invoices", false),
                    new("billing.write", "billing", "Billing", "إصدار الفواتير والتحصيل", "Create Invoices & Payments", false),
                    new("billing.discount", "billing", "Billing", "تطبيق الخصومات", "Apply Discounts", false),
                    new("billing.free_session", "billing", "Billing", "جلسات مجانية", "Authorize Free Sessions", false),
                    new("billing.custom_price", "billing", "Billing", "تعديل سعر الجلسة", "Override Session Price", false),
                    new("billing.extra_charge", "billing", "Billing", "إضافة رسوم إضافية", "Add Extra Charges", false),
                    new("billing.refund", "billing", "Billing", "إصدار المرتجعات", "Issue Refunds", false)
                ],
                MenuItems:
                [
                    new("billing.invoices", "billing", "الفواتير والتحصيل", "Billing", "/billing", "billing.read", 170)
                ],
                SettingsSections:
                [
                    new("billing-settings", "إعدادات الفواتير", "Billing Settings", "clinic.settings.edit", "billing")
                ],
                DashboardWidgets:
                [
                    new("billing.revenue-summary", "billing", "billing.read", "Clinic", "ملخص التحصيل", "Billing Revenue Summary"),
                    new("billing.outstanding-debts", "billing", "billing.read", "Clinic", "المديونيات المستحقة", "Outstanding Debts"),
                    new("billing.refunds-discounts", "billing", "billing.read", "Clinic", "الخصومات والمرتجعات", "Refunds & Discounts")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "finance",
                TitleAr: "المالية والخزينة وإغلاق اليوم",
                TitleEn: "Finance & Day Close",
                DescriptionAr: "الخزينة، المصروفات، وتقفيل اليومية.",
                DescriptionEn: "Cashbook, expenses, and end-of-day financial lock.",
                Prerequisites: Array.Empty<string>(),
                Permissions:
                [
                    new("finance.read", "finance", "Finance", "عرض الخزينة والمصروفات", "View Cashbook & Expenses", false),
                    new("finance.write", "finance", "Finance", "تسجيل المصروفات والموردين", "Record Expenses & Suppliers", false),
                    new("finance.close_day", "finance", "Finance", "إغلاق اليوم", "Close Day", false),
                    new("finance.reopen_day", "finance", "Finance", "إعادة فتح اليوم المغلق", "Reopen Closed Day", false)
                ],
                MenuItems:
                [
                    new("finance.cashbook", "finance", "المالية والخزينة", "Finance & Close Day", "/finance", "finance.read", 180),
                    new("finance.dashboard", "finance", "لوحة المالية", "Finance Dashboard", "/finance/dashboard", "finance.read", 181)
                ],
                SettingsSections: Array.Empty<SettingsSectionResult>(),
                DashboardWidgets:
                [
                    new("finance.cash-summary", "finance", "finance.read", "Clinic", "رصيد الخزينة", "Cash Summary"),
                    new("finance.closing-status", "finance", "finance.read", "Clinic", "حالة إغلاق اليوم", "Daily Closing Status"),
                    new("finance.income-vs-expenses", "finance", "finance.read", "Clinic", "الدخل مقابل المصروفات", "Income vs Expenses"),
                    new("finance.supplier-balances", "finance", "finance.read", "Clinic", "أرصدة الموردين", "Supplier Balances"),
                    new("finance.fixed-costs-due", "finance", "finance.read", "Clinic", "التكاليف الثابتة المستحقة", "Fixed Costs Due"),
                    new("finance.payroll-summary", "finance", "finance.read", "Clinic", "ملخص الرواتب", "Payroll Summary")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "treatment-plans",
                TitleAr: "خطط العلاج",
                TitleEn: "Treatment Plans",
                DescriptionAr: "تخطيط الجلسات المتعددة والتسعير المرحلي.",
                DescriptionEn: "Multi-session treatment plans and staged billing.",
                Prerequisites: ["patients", "scheduling", "billing"],
                Permissions:
                [
                    new("treatment_plans.read", "treatment-plans", "Treatment Plans", "عرض خطط العلاج", "View Treatment Plans", false),
                    new("treatment_plans.write", "treatment-plans", "Treatment Plans", "إعداد وتعديل خطط العلاج", "Manage Treatment Plans", false)
                ],
                MenuItems:
                [
                    new("treatment-plans.list", "treatment-plans", "خطط العلاج", "Treatment Plans", "/treatment-plans", "treatment_plans.read", 190),
                    new("treatment-plans.dashboard", "treatment-plans", "لوحة خطط العلاج", "Treatment Plans Dashboard", "/treatment-plans/dashboard", "treatment_plans.read", 191)
                ],
                SettingsSections: Array.Empty<SettingsSectionResult>(),
                DashboardWidgets:
                [
                    new("treatment-plans.active-plans", "treatment-plans", "treatment_plans.read", "Clinic", "الخطط العلاجية النشطة", "Active Treatment Plans"),
                    new("treatment-plans.completion-rate", "treatment-plans", "treatment_plans.read", "Clinic", "نسبة إنجاز الخطط", "Plan Completion Rate"),
                    new("treatment-plans.delayed-sessions", "treatment-plans", "treatment_plans.read", "Clinic", "الجلسات المتأخرة", "Delayed Plan Sessions"),
                    new("treatment-plans.installments-due", "treatment-plans", "treatment_plans.read", "Clinic", "الأقساط المستحقة والمتأخرة", "Installments Due & Overdue"),
                    new("treatment-plans.plan-revenue", "treatment-plans", "treatment_plans.read", "Clinic", "إيرادات خطط العلاج", "Treatment Plan Revenue")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "inventory",
                TitleAr: "المخزون والمستلزمات",
                TitleEn: "Inventory & Supplies",
                DescriptionAr: "متابعة الأصناف والكميات وحدود التنبيه.",
                DescriptionEn: "Stock tracking, lots, and low-stock alerts.",
                Prerequisites: Array.Empty<string>(),
                Permissions:
                [
                    new("inventory.read", "inventory", "Inventory", "عرض المخزون", "View Inventory", false),
                    new("inventory.write", "inventory", "Inventory", "حركات الصرف والتوريد", "Manage Stock Movements", false)
                ],
                MenuItems:
                [
                    new("inventory.items", "inventory", "المخزون", "Inventory", "/inventory", "inventory.read", 200),
                    new("inventory.dashboard", "inventory", "لوحة المخزون", "Inventory Dashboard", "/inventory/dashboard", "inventory.read", 205)
                ],
                SettingsSections:
                [
                    new("inventory-settings", "إعدادات المخزون", "Inventory Settings", "clinic.settings.edit", "inventory")
                ],
                DashboardWidgets:
                [
                    new("inventory.low-stock", "inventory", "inventory.read", "Clinic", "تنبيهات انخفاض المخزون", "Low Stock Items"),
                    new("inventory.near-expiry", "inventory", "inventory.read", "Clinic", "دفعات قريبة الانتهاء", "Near Expiry Batches"),
                    new("inventory.stock-value", "inventory", "inventory.read", "Clinic", "قيمة المخزون الحالية", "Current Stock Valuation"),
                    new("inventory.usage-by-item", "inventory", "inventory.read", "Clinic", "الاستهلاك حسب الصنف", "Tool Consumption Volume"),
                    new("inventory.negative-stock-events", "inventory", "inventory.read", "Clinic", "أحداث الرصيد السالب", "Negative Stock Events")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "time-engine",
                TitleAr: "محرك الزمن ومؤشرات التاخير",
                TitleEn: "Time & Delay Analytics",
                DescriptionAr: "تحليل أزمنة الانتظار ومعدلات الالتزام وحساب المدة المتكيفة لكل طبيب.",
                DescriptionEn: "Adaptive per-doctor session duration learning, overtime excuses, and waiting analytics.",
                Prerequisites: ["scheduling", "clinical"],
                Permissions:
                [
                    new("time_engine.read", "time-engine", "Time Analytics", "عرض محرك الزمن", "View Time Engine", false),
                    new("time_engine.manage", "time-engine", "Time Analytics", "إدارة إعدادات محرك الزمن", "Manage Time Engine Settings", false),
                    new("time_engine.dashboard.view", "time-engine", "Time Analytics", "عرض لوحة محرك الزمن", "View Time Engine Dashboard", false)
                ],
                MenuItems:
                [
                    new("time-engine.metrics", "time-engine", "محرك الزمن", "Time Engine", "/time-engine", "time_engine.read", 210),
                    new("time-engine.dashboard", "time-engine", "لوحة محرك الزمن", "Time Engine Dashboard", "/time-engine/dashboard", "time_engine.dashboard.view", 211)
                ],
                SettingsSections: Array.Empty<SettingsSectionResult>(),
                DashboardWidgets:
                [
                    new("time-engine.avg-wait", "time-engine", "time_engine.read", "Clinic", "متوسط الانتظار", "Average Wait Minutes"),
                    new("time-engine.suggested-vs-applied", "time-engine", "time_engine.dashboard.view", "Clinic", "المدة المقترحة مقابل المطبقة", "Suggested vs Applied Duration"),
                    new("time-engine.sample-counts", "time-engine", "time_engine.dashboard.view", "Clinic", "عينات محرك الزمن", "Time Engine Sample Counts"),
                    new("time-engine.adoption-rate", "time-engine", "time_engine.dashboard.view", "Clinic", "معدل اعتماد المحرك", "Time Engine Adoption Rate")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "reports",
                TitleAr: "التقارير والتصدير",
                TitleEn: "Reports & Exports",
                DescriptionAr: "التقارير المالية والتشغيلية والتصدير.",
                DescriptionEn: "Operational and financial analytics with PDF/CSV exports.",
                Prerequisites: Array.Empty<string>(),
                Permissions:
                [
                    new("reports.read", "reports", "Reports", "عرض التقارير", "View Reports", false),
                    new("reports.export", "reports", "Reports", "تصدير التقارير", "Export Reports", false)
                ],
                MenuItems:
                [
                    new("reports.center", "reports", "التقارير", "Reports", "/reports", "reports.read", 220),
                    new("reports.dashboard", "reports", "لوحة التقارير", "Reports Dashboard", "/reports/dashboard", "reports.read", 221)
                ],
                SettingsSections: Array.Empty<SettingsSectionResult>(),
                DashboardWidgets:
                [
                    new("reports.exports-run", "reports", "reports.read", "Clinic", "التقارير المصدرة", "Exports Run"),
                    new("reports.scheduled-reports", "reports", "reports.read", "Clinic", "التقارير المجدولة", "Scheduled Reports")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "excel-import",
                TitleAr: "استيراد ملفات إكسل",
                TitleEn: "Excel Data Import",
                DescriptionAr: "استيراد بيانات المرضى والخدمات من ملفات Excel.",
                DescriptionEn: "Bulk spreadsheet import for patients and catalogs.",
                Prerequisites: ["patients"],
                Permissions:
                [
                    new("excel_import.execute", "excel-import", "Import", "تنفيذ الاستيراد", "Execute Excel Import", false)
                ],
                MenuItems:
                [
                    new("excel-import.wizard", "excel-import", "استيراد البيانات", "Excel Import", "/import", "excel_import.execute", 230),
                    new("excel-import.dashboard", "excel-import", "لوحة الاستيراد", "Excel Import Dashboard", "/excel-import/dashboard", "excel_import.execute", 231)
                ],
                SettingsSections: Array.Empty<SettingsSectionResult>(),
                DashboardWidgets:
                [
                    new("excel-import.history", "excel-import", "excel_import.execute", "Clinic", "سجل الاستيراد", "Import History"),
                    new("excel-import.rejected-rows", "excel-import", "excel_import.execute", "Clinic", "الصفوف المرفوضة", "Rejected Import Rows")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "portal",
                TitleAr: "بوابة المريض",
                TitleEn: "Patient Portal",
                DescriptionAr: "عرض السجلات والنتائج وخطط العلاج للمريض.",
                DescriptionEn: "Patient self-service portal for visits, medical records, files, and treatment plans.",
                Prerequisites: ["patients"],
                Permissions:
                [
                    new("portal.invite", "portal", "Patient Portal", "دعوة وإدارة حسابات البوابة", "Invite & Manage Portal Accounts", false),
                    new("portal.manage", "portal", "Patient Portal", "إدارة بوابة المريض", "Manage Patient Portal Settings", false),
                    new("portal.dashboard.view", "portal", "Patient Portal", "عرض لوحة بوابة المريض", "View Portal Dashboard", false)
                ],
                MenuItems:
                [
                    new("portal.dashboard", "portal", "لوحة بوابة المريض", "Portal Dashboard", "/portal/dashboard", "portal.dashboard.view", 238),
                    new("portal.settings", "portal", "إعدادات بوابة المريض", "Patient Portal", "/portal-admin", "portal.manage", 240)
                ],
                SettingsSections:
                [
                    new("portal-settings", "إعدادات بوابة المريض", "Patient Portal Settings", "clinic.settings.edit", "portal")
                ],
                DashboardWidgets:
                [
                    new("portal.accounts-summary", "portal", "portal.dashboard.view", "Clinic", "حسابات بوابة المريض", "Portal Accounts Summary"),
                    new("portal.logins-week", "portal", "portal.dashboard.view", "Clinic", "تسجيلات الدخول الأسبوعية", "Portal Logins This Week"),
                    new("portal.failed-logins", "portal", "portal.dashboard.view", "Clinic", "محاولات الدخول الفاشلة", "Portal Failed Logins"),
                    new("portal.record-views-month", "portal", "portal.dashboard.view", "Clinic", "قراءات السجلات هذا الشهر", "Patients Viewing Records This Month"),
                    new("portal.access-log-summary", "portal", "portal.dashboard.view", "Clinic", "ملخص سجل وصول البوابة", "Portal Access Log Summary")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "portal-booking",
                TitleAr: "حجز المواعيد من البوابة",
                TitleEn: "Portal Online Booking",
                DescriptionAr: "تمكين المرضى من حجز وإلغاء المواعيد ذاتياً ومراجعة الطلبات.",
                DescriptionEn: "Allow patients to self-book and cancel appointments online.",
                Prerequisites: ["portal", "scheduling"],
                Permissions:
                [
                    new("portal.booking.manage", "portal-booking", "Patient Portal", "إدارة الحجز الذاتي", "Manage Portal Self-Booking", false),
                    new("portal.booking.requests.review", "portal-booking", "Patient Portal", "مراجعة طلبات الحجز", "Review Portal Booking Requests", false),
                    new("portal.booking.dashboard.view", "portal-booking", "Patient Portal", "عرض لوحة الحجز الذاتي", "View Portal Booking Dashboard", false)
                ],
                MenuItems:
                [
                    new("portal-booking.dashboard", "portal-booking", "لوحة الحجز الذاتي", "Portal Booking Dashboard", "/portal-booking/dashboard", "portal.booking.dashboard.view", 242),
                    new("portal-booking.requests", "portal-booking", "طلبات الحجز من البوابة", "Portal Booking Requests", "/portal-booking/requests", "portal.booking.requests.review", 244)
                ],
                SettingsSections:
                [
                    new("portal-booking-settings", "إعدادات الحجز الذاتي من البوابة", "Portal Booking Rules", "clinic.settings.edit", "portal-booking")
                ],
                DashboardWidgets:
                [
                    new("portal-booking.daily-count", "portal-booking", "portal.booking.dashboard.view", "Clinic", "حجوزات البوابة", "Portal Bookings Count"),
                    new("portal-booking.cancellations", "portal-booking", "portal.booking.dashboard.view", "Clinic", "إلغاءات البوابة", "Portal Cancellations"),
                    new("portal-booking.pending-requests", "portal-booking", "portal.booking.dashboard.view", "Clinic", "طلبات الحجز المعلقة", "Pending Approval Requests"),
                    new("portal-booking.no-shows", "portal-booking", "portal.booking.dashboard.view", "Clinic", "حالات عدم الحضور من البوابة", "Portal No-Shows & Blocks"),
                    new("portal-booking.slot-utilization", "portal-booking", "portal.booking.dashboard.view", "Clinic", "استخدام الحصة اليومية للبوابة", "Portal Daily Cap Utilization (%)"),
                    new("portal-booking.portal-share", "portal-booking", "portal.booking.dashboard.view", "Clinic", "نسبة الحجز الذاتي من البوابة", "Portal Share of Total Bookings (%)")
                ]),

            new ModuleManifestEntry(
                ModuleKey: "offline-sync",
                TitleAr: "المزامنة دون اتصال",
                TitleEn: "Offline Sync",
                DescriptionAr: "العمل دون إنترنت والمزامنة التلقائية وحل التعارضات.",
                DescriptionEn: "IndexedDB offline queue, background synchronization, and conflict resolution.",
                Prerequisites: Array.Empty<string>(),
                Permissions:
                [
                    new("offline_sync.use", "offline-sync", "Offline Sync", "استخدام المزامنة دون اتصال", "Use Offline Sync", false),
                    new("offline_sync.resolve", "offline-sync", "Offline Sync", "حل تعارضات المزامنة", "Resolve Sync Conflicts", false),
                    new("offline_sync.settings", "offline-sync", "Offline Sync", "إدارة إعدادات المزامنة", "Manage Sync Settings", false)
                ],
                MenuItems:
                [
                    new("offline-sync.dashboard", "offline-sync", "لوحة المزامنة والتعارضات", "Offline Sync & Conflicts", "/offline-sync/dashboard", "offline_sync.use", 250)
                ],
                SettingsSections:
                [
                    new("offline-sync-settings", "إعدادات المزامنة دون اتصال", "Offline Sync Settings", "offline_sync.settings", "offline-sync")
                ],
                DashboardWidgets:
                [
                    new("offline-sync.registered-devices", "offline-sync", "offline_sync.use", "Clinic", "الأجهزة المسجلة للمزامنة", "Registered Sync Devices"),
                    new("offline-sync.last-sync", "offline-sync", "offline_sync.use", "Clinic", "آخر مزامنة", "Last Successful Sync"),
                    new("offline-sync.pending-operations", "offline-sync", "offline_sync.use", "Clinic", "العمليات المعلقة", "Pending Offline Operations"),
                    new("offline-sync.open-conflicts", "offline-sync", "offline_sync.use", "Clinic", "تعارضات المزامنة المفتوحة", "Open Sync Conflicts")
                ])
        ];
    }
}
