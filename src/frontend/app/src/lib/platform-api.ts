import { apiRequest } from "./api";

export type SystemSchoolSummary = {
  id: string;
  legalName: string;
  displayName: string;
  slug: string;
  logoDataUrl?: string;
  baseBeachName?: string;
  baseLatitude?: number;
  baseLongitude?: number;
  status: string;
  timezone: string;
  currencyCode: string;
  createdAtUtc: string;
  usersCount: number;
  ownerName?: string;
};

export type SystemSchoolDetails = {
  id: string;
  legalName: string;
  displayName: string;
  cnpj?: string;
  baseBeachName?: string;
  baseLatitude?: number;
  baseLongitude?: number;
  logoDataUrl?: string;
  postalCode?: string;
  street?: string;
  streetNumber?: string;
  addressComplement?: string;
  neighborhood?: string;
  city?: string;
  state?: string;
  timezone: string;
  currencyCode: string;
  status: string;
  owner?: {
    id: string;
    identityUserId: string;
    fullName: string;
    cpf?: string;
    phone?: string;
    postalCode?: string;
    street?: string;
    streetNumber?: string;
    addressComplement?: string;
    neighborhood?: string;
    city?: string;
    state?: string;
    isActive: boolean;
  } | null;
};

export type CreateSystemSchoolPayload = {
  legalName: string;
  displayName: string;
  cnpj?: string;
  baseBeachName: string;
  baseLatitude?: number;
  baseLongitude?: number;
  postalCode: string;
  street: string;
  streetNumber: string;
  addressComplement?: string;
  neighborhood: string;
  city: string;
  state: string;
  ownerFullName: string;
  ownerEmail: string;
  ownerCpf: string;
  ownerPhone?: string;
  ownerPostalCode: string;
  ownerStreet: string;
  ownerStreetNumber: string;
  ownerAddressComplement?: string;
  ownerNeighborhood: string;
  ownerCity: string;
  ownerState: string;
  slug?: string;
  timezone?: string;
  currencyCode?: string;
  logoDataUrl?: string;
  themePrimary?: string;
  themeAccent?: string;
  bookingLeadTimeMinutes?: number;
  cancellationWindowHours?: number;
};

export type UpdateSystemSchoolPayload = {
  legalName: string;
  displayName: string;
  cnpj?: string;
  baseBeachName: string;
  baseLatitude?: number;
  baseLongitude?: number;
  logoDataUrl?: string;
  postalCode: string;
  street: string;
  streetNumber: string;
  addressComplement?: string;
  neighborhood: string;
  city: string;
  state: string;
  ownerFullName: string;
  ownerCpf: string;
  ownerPhone?: string;
  ownerPostalCode: string;
  ownerStreet: string;
  ownerStreetNumber: string;
  ownerAddressComplement?: string;
  ownerNeighborhood: string;
  ownerCity: string;
  ownerState: string;
  ownerIsActive: boolean;
  status: string;
  timezone?: string;
  currencyCode?: string;
};

export function getSystemSchools(token: string) {
  return apiRequest<SystemSchoolSummary[]>("/api/v1/system/schools", {
    token
  });
}

export function createSystemSchool(token: string, payload: CreateSystemSchoolPayload) {
  return apiRequest<{
    schoolId: string;
    ownerUserId: string;
    createdAtUtc: string;
    temporaryPasswordSent: boolean;
    deliveryMode: string;
    outboxFilePath?: string;
  }>("/api/v1/system/schools", {
    method: "POST",
    token,
    body: payload
  });
}

export function getSystemSchoolDetails(token: string, schoolId: string) {
  return apiRequest<SystemSchoolDetails>(`/api/v1/system/schools/${schoolId}`, {
    token
  });
}

export function updateSystemSchool(token: string, schoolId: string, payload: UpdateSystemSchoolPayload) {
  return apiRequest<{ updatedAtUtc: string; id: string }>(`/api/v1/system/schools/${schoolId}`, {
    method: "PUT",
    token,
    body: payload
  });
}

export function deleteSystemSchool(token: string, schoolId: string) {
  return apiRequest<{ schoolId: string; deletedAtUtc: string }>(`/api/v1/system/schools/${schoolId}`, {
    method: "DELETE",
    token
  });
}

export type Student = {
  id: string;
  fullName: string;
  email?: string;
  phone?: string;
  postalCode?: string;
  street?: string;
  streetNumber?: string;
  addressComplement?: string;
  neighborhood?: string;
  city?: string;
  state?: string;
  identityUserId?: string;
  birthDate?: string;
  medicalNotes?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  firstStandUpAtUtc?: string;
  activeEnrollments?: number;
  realizedLessons?: number;
  upcomingLessons?: number;
  noShowCount?: number;
  progressPercent?: number;
  isActive: boolean;
  createdAtUtc: string;
};

export type Instructor = {
  id: string;
  fullName: string;
  email?: string;
  phone?: string;
  specialties?: string;
  availability: InstructorAvailabilitySlot[];
  hourlyRate: number;
  identityUserId?: string;
  isActive: boolean;
  createdAtUtc: string;
};

export type InstructorAvailabilitySlot = {
  dayOfWeek: number;
  startMinutesUtc: number;
  endMinutesUtc: number;
  label: string;
};

export type Course = {
  id: string;
  name: string;
  level: string;
  totalMinutes: number;
  totalHours: number;
  price: number;
  isActive: boolean;
  pedagogicalTrack: Array<{
    id: string;
    title: string;
    focus: string;
    estimatedHours: number;
  }>;
};

export type Enrollment = {
  id: string;
  studentId: string;
  studentName: string;
  courseId: string;
  courseName: string;
  status: string;
  includedMinutesSnapshot: number;
  usedMinutes: number;
  remainingMinutes: number;
  coursePriceSnapshot: number;
  startedAtUtc: string;
  endedAtUtc?: string;
  progressPercent: number;
  realizedLessons: number;
  noShowCount: number;
  currentModule: string;
};

export type EnrollmentLedgerEntry = {
  id: string;
  lessonId?: string;
  deltaMinutes: number;
  reason: string;
  occurredAtUtc: string;
};

export type Lesson = {
  id: string;
  schoolId: string;
  kind: string;
  status: string;
  studentId: string;
  studentName: string;
  instructorId: string;
  instructorName: string;
  enrollmentId?: string;
  singleLessonPrice?: number;
  startAtUtc: string;
  durationMinutes: number;
  notes?: string;
  operationalConfirmedAtUtc?: string;
  operationalConfirmedByUserId?: string;
  operationalConfirmationNote?: string;
  noShowMarkedAtUtc?: string;
  noShowMarkedByUserId?: string;
  noShowNote?: string;
};

export type ScheduleBlock = {
  id: string;
  scope: string;
  instructorId?: string;
  instructorName?: string;
  title: string;
  notes?: string;
  startAtUtc: string;
  endAtUtc: string;
};

export type AssistedLessonSuggestion = {
  startAtUtc: string;
  endAtUtc: string;
  instructorId: string;
  instructorName: string;
  availabilityLabel: string;
};

export type StudentPortalOverview = {
  student: {
    id: string;
    fullName: string;
    email?: string;
    phone?: string;
    firstStandUpAtUtc?: string;
  };
  summary: {
    totalRealizedLessons: number;
    totalUpcomingLessons: number;
    activeEnrollments: number;
    completedCourses: number;
    trainingStage: string;
    profileCompleteness: number;
  };
  portalRules: {
    bookingLeadTimeMinutes: number;
    cancellationWindowHours: number;
    rescheduleWindowHours: number;
    attendanceConfirmationLeadMinutes: number;
    lessonReminderLeadHours: number;
    portalNotificationsEnabled: boolean;
  };
  progress: {
    readinessScore: number;
    currentFocus: string;
    recommendedNextStep: string;
    lastTrainingAtUtc?: string;
    tracks: Array<{
      id: string;
      title: string;
      description: string;
        status: string;
        progressPercent: number;
        achievedAtUtc?: string;
      }>;
    modules: Array<{
      id: string;
      title: string;
      description: string;
      status: string;
      progressPercent: number;
      skills: Array<{
        title: string;
        status: string;
        progressPercent: number;
      }>;
    }>;
    milestones: Array<{
      title: string;
      description: string;
      status: string;
      occurredAtUtc?: string;
    }>;
  };
  notifications: {
    unreadCount: number;
    items: StudentPortalNotification[];
  };
  enrollments: Array<{
    id: string;
    courseId: string;
    courseName: string;
    level: string;
    status: string;
    includedMinutesSnapshot: number;
    usedMinutes: number;
    remainingMinutes: number;
    scheduledMinutes: number;
    availableToScheduleMinutes: number;
    progressPercent: number;
    startedAtUtc: string;
    endedAtUtc?: string;
  }>;
  upcomingLessons: Array<{
    id: string;
    kind: string;
    status: string;
    startAtUtc: string;
    durationMinutes: number;
    notes?: string;
    studentConfirmedAtUtc?: string;
    studentConfirmationNote?: string;
    minutesUntilStart: number;
    canCancel: boolean;
    canReschedule: boolean;
    canConfirmPresence: boolean;
    instructor: {
      instructorId: string;
      name: string;
    };
    enrollment?: {
      enrollmentId: string;
      courseId: string;
      courseName: string;
    } | null;
  }>;
  lessonHistory: Array<{
    id: string;
    kind: string;
    status: string;
    startAtUtc: string;
    durationMinutes: number;
    notes?: string;
    studentConfirmedAtUtc?: string;
    studentConfirmationNote?: string;
    instructorName: string;
    courseName?: string;
    sessionTitle: string;
    evolutionSummary: string;
    statusMessage: string;
  }>;
  instructors: Array<{
    id: string;
    fullName: string;
    specialties?: string;
  }>;
};

export type StudentPortalNotification = {
  id: string;
  category: string;
  title: string;
  message: string;
  actionLabel?: string;
  actionPath?: string;
  createdAtUtc: string;
  readAtUtc?: string;
  isSynthetic: boolean;
};

export type StudentPortalHistoryResponse = {
  student: {
    id: string;
    fullName: string;
  };
  items: Array<{
    id: string;
    kind: string;
    status: string;
    startAtUtc: string;
    durationMinutes: number;
    notes?: string;
    studentConfirmedAtUtc?: string;
    studentConfirmationNote?: string;
    instructorName: string;
    courseName?: string;
    sessionTitle: string;
    timelineLabel: string;
    evolutionSummary: string;
    statusMessage: string;
  }>;
};

export type StudentPortalProfileResponse = {
  student: {
    id: string;
    fullName: string;
    email?: string;
    phone?: string;
    birthDate?: string;
    medicalNotes?: string;
    emergencyContactName?: string;
    emergencyContactPhone?: string;
    firstStandUpAtUtc?: string;
    createdAtUtc: string;
  };
  summary: {
    profileCompleteness: number;
    realizedLessons: number;
    upcomingLessons: number;
  };
};

export type StudentPortalNotificationsResponse = {
  unreadCount: number;
  items: StudentPortalNotification[];
};

export type Storage = {
  id: string;
  name: string;
  locationNote?: string;
  isActive: boolean;
};

export type EquipmentItem = {
  id: string;
  name: string;
  type: string;
  category?: string;
  tagCode?: string;
  brand?: string;
  model?: string;
  sizeLabel?: string;
  condition: string;
  totalUsageMinutes: number;
  lastServiceDateUtc?: string;
  lastServiceUsageMinutes?: number;
  isActive: boolean;
  storageId: string;
  storageName: string;
  ownershipType: string;
  ownerDisplayName?: string;
  totalUsageHours?: number;
  availabilityStatus?: string;
  reservedLessonId?: string;
  reservedFromUtc?: string;
  reservedUntilUtc?: string;
  reservedLessonLabel?: string;
  isCheckedOut?: boolean;
  kitId?: string;
  kitName?: string;
};

export type EquipmentHistory = {
  equipment: EquipmentItem & {
    kitId?: string;
    kitName?: string;
  };
  usage: Array<{
    id: string;
    lessonId?: string;
    checkoutItemId: string;
    usageMinutes: number;
    conditionAfter: string;
    recordedAtUtc: string;
  }>;
  maintenance: Array<{
    id: string;
    serviceDateUtc: string;
    usageMinutesAtService: number;
    cost?: number;
    description: string;
    performedBy?: string;
    serviceCategory: string;
    financialEffect: string;
    counterpartyName?: string;
  }>;
  reservations: Array<{
    id: string;
    lessonId: string;
    reservedFromUtc: string;
    reservedUntilUtc: string;
    notes?: string;
  }>;
  lifecycle: {
    usageMinutes: number;
    servicesCount: number;
    reservationsCount: number;
    maintenanceExpense: number;
    maintenanceRevenue: number;
    timeline: Array<{
      atUtc: string;
      kind: string;
      title: string;
      detail: string;
    }>;
  };
};

export type EquipmentKit = {
  id: string;
  name: string;
  description?: string;
  isActive: boolean;
  items: Array<{
    equipmentId: string;
    name: string;
    type: string;
  }>;
};

export type MaintenanceRule = {
  id: string;
  equipmentType: string;
  planName: string;
  serviceCategory: string;
  serviceEveryMinutes?: number;
  serviceEveryDays?: number;
  warningLeadMinutes?: number;
  criticalLeadMinutes?: number;
  warningLeadDays?: number;
  criticalLeadDays?: number;
  checklist?: string;
  notes?: string;
  isActive: boolean;
};

export type MaintenanceRecord = {
  id: string;
  equipmentId: string;
  equipmentName: string;
  equipmentType: string;
  equipmentOwnershipType: string;
  serviceDateUtc: string;
  usageMinutesAtService: number;
  cost?: number;
  description: string;
  performedBy?: string;
  serviceCategory: string;
  financialEffect: string;
  counterpartyName?: string;
};

export type MaintenanceAlert = {
  id: string;
  name: string;
  type: string;
  alertType: string;
  severity: string;
  serviceCategory: string;
  recommendedAction: string;
  remainingMinutes?: number;
  dueDateUtc?: string;
  remainingDays?: number;
  condition?: string;
};

export type LessonEquipmentState = {
  checkout?: {
    id: string;
    lessonId: string;
    checkedOutAtUtc: string;
    checkedInAtUtc?: string;
    notesBefore?: string;
    notesAfter?: string;
    createdByUserId: string;
    checkedInByUserId?: string;
  };
  reservation?: {
    id: string;
    lessonId: string;
    reservedFromUtc: string;
    reservedUntilUtc: string;
    notes?: string;
    createdByUserId: string;
  };
  reservedItems: Array<{
    id: string;
    equipmentId: string;
    equipmentName: string;
    equipmentType: string;
  }>;
  items: Array<{
    id: string;
    equipmentId: string;
    equipmentName: string;
    equipmentType: string;
    conditionBefore: string;
    conditionAfter?: string;
    notesBefore?: string;
    notesAfter?: string;
  }>;
};

export type MaintenanceSummary = {
  records: number;
  expenseAmount: number;
  revenueAmount: number;
  byCategory: Array<{
    category: string;
    records: number;
    amount: number;
  }>;
  byEquipment: Array<{
    equipmentId: string;
    equipmentName: string;
    records: number;
    amount: number;
  }>;
};

export type DashboardReport = {
  generatedAtUtc: string;
  fromUtc?: string;
  toUtc?: string;
  serviceErrors?: Array<{
    service: string;
    statusCode: number;
    message: string;
  }>;
  academics?: {
    fromUtc?: string;
    toUtc?: string;
    students: number;
    instructors: number;
    courses: number;
    activeEnrollments: number;
    scheduledLessons: number;
    realizedLessons: number;
    totalLessonsInPeriod: number;
    completionRate: number;
    lessonsToday: number;
    statusBreakdown: Array<{ status: string; count: number }>;
    lessonSeries: Array<{
      bucketStartUtc: string;
      bucketLabel: string;
      totalLessons: number;
      realizedLessons: number;
      cancelledLessons: number;
    }>;
    instructorPerformance: Array<{
      instructorId: string;
      instructorName: string;
      total: number;
      realized: number;
      noShow: number;
      cancelled: number;
      rescheduled: number;
    }>;
    invariants: string[];
  };
  equipment?: {
    fromUtc?: string;
    toUtc?: string;
    storages: number;
    equipment: number;
    equipmentInAttention: number;
    openCheckouts: number;
    pendingMaintenance: number;
    usageMinutesInPeriod: number;
    checkoutsInPeriod: number;
    maintenanceExecutedInPeriod: number;
    conditionBreakdown: Array<{
      condition: string;
      count: number;
    }>;
    activitySeries: Array<{
      bucketStartUtc: string;
      bucketLabel: string;
      usageMinutes: number;
      checkouts: number;
      maintenanceRecords: number;
    }>;
  };
  maintenanceAlerts?: MaintenanceAlert[];
  finance?: {
    fromUtc?: string;
    toUtc?: string;
    totalRevenue: number;
    manualExpenseTotal: number;
    instructorPayrollExpense: number;
    realizedInstructionMinutes: number;
    totalExpense: number;
    grossMargin: number;
    revenueEntries: number;
    expenseEntries: number;
    revenueBySource: RevenueBreakdown[];
    expenseByCategory: ExpenseBreakdown[];
    cashflowSeries: Array<{
      bucketStartUtc: string;
      bucketLabel: string;
      revenue: number;
      expense: number;
      net: number;
    }>;
  };
};

export type FinancialReport = {
  generatedAtUtc: string;
  finance?: {
    totalRevenue: number;
    totalExpense: number;
    grossMargin: number;
    revenueEntries: number;
    expenseEntries: number;
  };
};

export type RevenueBreakdown = {
  sourceType: string;
  totalAmount: number;
  entries: number;
};

export type ExpenseBreakdown = {
  category: string;
  totalAmount: number;
  entries: number;
};

export type FinanceOverview = {
  fromUtc?: string;
  toUtc?: string;
  totalRevenue: number;
  manualExpenseTotal: number;
  instructorPayrollExpense: number;
  realizedInstructionMinutes: number;
  totalExpense: number;
  grossMargin: number;
  receivablesOpenAmount: number;
  receivablesOverdueAmount: number;
  receivablesOpenEntries: number;
  delinquentStudents: number;
  dueSoonStudents: number;
  revenueEntries: number;
  expenseEntries: number;
  revenueBySource: RevenueBreakdown[];
  expenseByCategory: ExpenseBreakdown[];
};

export type StudentFinancialStatusSummary = {
  studentId: string;
  studentName: string;
  status: string;
  openAmount: number;
  overdueAmount: number;
  openReceivables: number;
  overdueReceivables: number;
  nextDueAtUtc?: string;
};

export type StudentFinancialStatusesResponse = {
  delinquentStudents: number;
  dueSoonStudents: number;
  items: StudentFinancialStatusSummary[];
};

export type ReceivableEntry = {
  id: string;
  studentId: string;
  enrollmentId?: string;
  studentNameSnapshot: string;
  description: string;
  notes?: string;
  amount: number;
  paidAmount: number;
  remainingAmount: number;
  dueAtUtc: string;
  lastPaymentAtUtc?: string;
  status: string;
  isOverdue: boolean;
  paymentsCount: number;
  createdAtUtc: string;
};

export type RevenueEntry = {
  id: string;
  sourceType: string;
  sourceTypeCode: number;
  sourceId?: string;
  category: string;
  amount: number;
  recognizedAtUtc: string;
  description: string;
  createdAtUtc: string;
};

export type ExpenseEntry = {
  id: string;
  category: string;
  categoryCode: number;
  amount: number;
  occurredAtUtc: string;
  description: string;
  vendor?: string;
  createdAtUtc: string;
};

export type SchoolUser = {
  profileId: string;
  identityUserId: string;
  fullName: string;
  phone?: string;
  avatarUrl?: string;
  profileIsActive: boolean;
  email?: string;
  role?: string;
  permissions: string[];
  isActive: boolean;
  mustChangePassword: boolean;
  createdAtUtc: string;
  lastLoginAtUtc?: string;
};

export type SchoolInvitation = {
  id: string;
  email: string;
  fullName: string;
  phone?: string;
  role: string;
  expiresAtUtc: string;
  createdAtUtc: string;
  status: string;
  deliveryMode?: string;
  inviteLink?: string;
  outboxFilePath?: string;
};

export function getStudents(token: string) {
  return apiRequest<Student[]>("/academics/api/v1/students", { token });
}

export function createStudent(token: string, body: {
  fullName: string;
  email?: string;
  phone?: string;
  postalCode?: string;
  street?: string;
  streetNumber?: string;
  addressComplement?: string;
  neighborhood?: string;
  city?: string;
  state?: string;
  identityUserId?: string | null;
  birthDate?: string | null;
  medicalNotes?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  firstStandUpAtUtc?: string | null;
}) {
  return apiRequest<{ studentId: string }>("/academics/api/v1/students", {
    method: "POST",
    token,
    body
  });
}

export function updateStudent(token: string, studentId: string, body: {
  fullName: string;
  email?: string;
  phone?: string;
  postalCode?: string;
  street?: string;
  streetNumber?: string;
  addressComplement?: string;
  neighborhood?: string;
  city?: string;
  state?: string;
  identityUserId?: string | null;
  birthDate?: string | null;
  medicalNotes?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  firstStandUpAtUtc?: string | null;
  isActive: boolean;
}) {
  return apiRequest<void>(`/academics/api/v1/students/${studentId}`, {
    method: "PUT",
    token,
    body
  });
}

export function getInstructors(token: string) {
  return apiRequest<Instructor[]>("/academics/api/v1/instructors", { token });
}

export function createInstructor(token: string, body: {
  fullName: string;
  email?: string;
  phone?: string;
  specialties?: string;
  availability?: InstructorAvailabilitySlot[];
  hourlyRate: number;
  identityUserId?: string | null;
}) {
  return apiRequest<{ instructorId: string }>("/academics/api/v1/instructors", {
    method: "POST",
    token,
    body
  });
}

export function updateInstructor(token: string, instructorId: string, body: {
  fullName: string;
  email?: string;
  phone?: string;
  specialties?: string;
  availability?: InstructorAvailabilitySlot[];
  hourlyRate: number;
  identityUserId?: string | null;
  isActive: boolean;
}) {
  return apiRequest<void>(`/academics/api/v1/instructors/${instructorId}`, {
    method: "PUT",
    token,
    body
  });
}

export function getCourses(token: string) {
  return apiRequest<Course[]>("/academics/api/v1/courses", { token });
}

export function createCourse(token: string, body: {
  name: string;
  level: number;
  totalHours: number;
  price: number;
}) {
  return apiRequest<{ courseId: string }>("/academics/api/v1/courses", {
    method: "POST",
    token,
    body
  });
}

export function getEnrollments(token: string) {
  return apiRequest<Enrollment[]>("/academics/api/v1/enrollments", { token });
}

export function createEnrollment(token: string, body: {
  studentId: string;
  courseId: string;
  startedAtUtc?: string | null;
}) {
  return apiRequest<{
    enrollmentId: string;
    includedMinutesSnapshot: number;
    coursePriceSnapshot: number;
  }>("/academics/api/v1/enrollments", {
    method: "POST",
    token,
    body
  });
}

export function getEnrollmentLedger(token: string, enrollmentId: string) {
  return apiRequest<EnrollmentLedgerEntry[]>(`/academics/api/v1/enrollments/${enrollmentId}/ledger`, {
    token
  });
}

export function updateEnrollmentStatus(token: string, enrollmentId: string, body: {
  status: number;
  endedAtUtc?: string | null;
}) {
  return apiRequest<void>(`/academics/api/v1/enrollments/${enrollmentId}/status`, {
    method: "PATCH",
    token,
    body
  });
}

export function getLessons(token: string) {
  return apiRequest<Lesson[]>("/academics/api/v1/lessons", { token });
}

export function getScheduleBlocks(token: string) {
  return apiRequest<ScheduleBlock[]>("/academics/api/v1/lessons/schedule-blocks", { token });
}

export function createScheduleBlock(token: string, body: {
  scope: number;
  instructorId?: string | null;
  title: string;
  notes?: string;
  startAtUtc: string;
  endAtUtc: string;
}) {
  return apiRequest<{ blockId: string }>("/academics/api/v1/lessons/schedule-blocks", {
    method: "POST",
    token,
    body
  });
}

export function deleteScheduleBlock(token: string, blockId: string) {
  return apiRequest<void>(`/academics/api/v1/lessons/schedule-blocks/${blockId}`, {
    method: "DELETE",
    token
  });
}

export function getStudentPortalOverview(token: string) {
  return apiRequest<StudentPortalOverview>("/academics/api/v1/student-portal/overview", { token });
}

export function getStudentPortalHistory(token: string) {
  return apiRequest<StudentPortalHistoryResponse>("/academics/api/v1/student-portal/history", { token });
}

export function getStudentPortalProfile(token: string) {
  return apiRequest<StudentPortalProfileResponse>("/academics/api/v1/student-portal/profile", { token });
}

export function updateStudentPortalProfile(token: string, body: {
  fullName: string;
  phone?: string;
  birthDate?: string | null;
  medicalNotes?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
}) {
  return apiRequest<{ updatedAtUtc: string; profileCompleteness: number }>("/academics/api/v1/student-portal/profile", {
    method: "PUT",
    token,
    body
  });
}

export function getStudentPortalNotifications(token: string) {
  return apiRequest<StudentPortalNotificationsResponse>("/academics/api/v1/student-portal/notifications", { token });
}

export function readStudentPortalNotification(token: string, notificationId: string) {
  return apiRequest<{ id: string; readAtUtc: string }>(`/academics/api/v1/student-portal/notifications/${notificationId}/read`, {
    method: "POST",
    token
  });
}

export function readAllStudentPortalNotifications(token: string) {
  return apiRequest<{ readCount: number; readAtUtc: string }>("/academics/api/v1/student-portal/notifications/read-all", {
    method: "POST",
    token
  });
}

export function scheduleStudentCourseLesson(token: string, body: {
  enrollmentId: string;
  instructorId: string;
  startAtUtc: string;
  durationMinutes: number;
  notes?: string;
}) {
  return apiRequest<{ lessonId: string }>("/academics/api/v1/student-portal/lessons/course", {
    method: "POST",
    token,
    body
  });
}

export function rescheduleStudentLesson(token: string, lessonId: string, body: {
  instructorId: string;
  startAtUtc: string;
  durationMinutes: number;
  notes?: string;
  reason?: string;
}) {
  return apiRequest<{ previousLessonId: string; newLessonId: string }>(
    `/academics/api/v1/student-portal/lessons/${lessonId}/reschedule`,
    {
      method: "POST",
      token,
      body
    }
  );
}

export function cancelStudentLesson(token: string, lessonId: string, body: { reason?: string }) {
  return apiRequest<{ lessonId: string; status: string }>(
    `/academics/api/v1/student-portal/lessons/${lessonId}/cancel`,
    {
      method: "POST",
      token,
      body
    }
  );
}

export function confirmStudentLessonPresence(token: string, lessonId: string, body: { note?: string }) {
  return apiRequest<{
    lessonId: string;
    studentConfirmedAtUtc?: string;
    studentConfirmationNote?: string;
    status: string;
  }>(`/academics/api/v1/student-portal/lessons/${lessonId}/confirm-presence`, {
    method: "POST",
    token,
    body
  });
}

export function createLesson(token: string, body: {
  studentId: string;
  instructorId: string;
  kind: number;
  status: number;
  enrollmentId?: string | null;
  singleLessonPrice?: number | null;
  startAtUtc: string;
  durationMinutes: number;
  notes?: string;
}) {
  return apiRequest<{ lessonId: string }>("/academics/api/v1/lessons", {
    method: "POST",
    token,
    body
  });
}

export function updateLesson(token: string, lessonId: string, body: {
  studentId: string;
  instructorId: string;
  kind: number;
  status: number;
  enrollmentId?: string | null;
  singleLessonPrice?: number | null;
  startAtUtc: string;
  durationMinutes: number;
  notes?: string;
}) {
  return apiRequest<void>(`/academics/api/v1/lessons/${lessonId}`, {
    method: "PUT",
    token,
    body
  });
}

export function operationalConfirmLesson(token: string, lessonId: string, body: { note?: string }) {
  return apiRequest<{ confirmedAtUtc: string; lessonStatus: string }>(`/academics/api/v1/lessons/${lessonId}/operational-confirm`, {
    method: "POST",
    token,
    body
  });
}

export function markLessonNoShow(token: string, lessonId: string, body: { note?: string }) {
  return apiRequest<{
    noShowAtUtc: string;
    consumesCourseMinutes: boolean;
    chargesSingleLesson: boolean;
  }>(`/academics/api/v1/lessons/${lessonId}/mark-no-show`, {
    method: "POST",
    token,
    body
  });
}

export function getAssistedLessonSuggestions(token: string, lessonId: string, body: {
  startSearchAtUtc?: string | null;
  daysToSearch?: number;
  instructorId?: string | null;
}) {
  return apiRequest<{ lessonId: string; slots: AssistedLessonSuggestion[] }>(`/academics/api/v1/lessons/${lessonId}/assisted-rebook`, {
    method: "POST",
    token,
    body
  });
}

export function batchRescheduleLessons(token: string, body: {
  lessonIds: string[];
  newStartAtUtc: string;
  instructorId?: string | null;
}) {
  return apiRequest<{ rescheduled: number; newBaseStartAtUtc: string }>("/academics/api/v1/lessons/reschedule-batch", {
    method: "POST",
    token,
    body
  });
}

export function getStorages(token: string) {
  return apiRequest<Storage[]>("/equipment/api/v1/storages", { token });
}

export function createStorage(token: string, body: { name: string; locationNote?: string }) {
  return apiRequest<{ storageId: string }>("/equipment/api/v1/storages", {
    method: "POST",
    token,
    body
  });
}

export function getEquipment(token: string) {
  return apiRequest<EquipmentItem[]>("/equipment/api/v1/equipment-items", { token });
}

export function createEquipment(token: string, body: {
  storageId: string;
  name: string;
  type: number;
  category?: string;
  tagCode?: string;
  brand?: string;
  model?: string;
  sizeLabel?: string;
  currentCondition: number;
  ownershipType: number;
  ownerDisplayName?: string;
}) {
  return apiRequest<{ equipmentId: string }>("/equipment/api/v1/equipment-items", {
    method: "POST",
    token,
    body
  });
}

export function getEquipmentAvailability(token: string, filters: { fromUtc: string; toUtc: string }) {
  return apiRequest<EquipmentItem[]>(withQuery("/equipment/api/v1/equipment-items/availability", filters), { token });
}

export function getEquipmentHistory(token: string, equipmentId: string) {
  return apiRequest<EquipmentHistory>(`/equipment/api/v1/equipment-items/${equipmentId}/history`, {
    token
  });
}

export function getEquipmentKits(token: string) {
  return apiRequest<EquipmentKit[]>("/equipment/api/v1/equipment-kits", { token });
}

export function createEquipmentKit(token: string, body: {
  name: string;
  description?: string;
  equipmentIds: string[];
}) {
  return apiRequest<{ kitId: string }>("/equipment/api/v1/equipment-kits", {
    method: "POST",
    token,
    body
  });
}

export function getMaintenanceRules(token: string) {
  return apiRequest<MaintenanceRule[]>("/equipment/api/v1/maintenance/rules", { token });
}

export function upsertMaintenanceRule(token: string, body: {
  equipmentType: number;
  planName?: string;
  serviceCategory: number;
  serviceEveryMinutes?: number | null;
  serviceEveryDays?: number | null;
  warningLeadMinutes?: number | null;
  criticalLeadMinutes?: number | null;
  warningLeadDays?: number | null;
  criticalLeadDays?: number | null;
  checklist?: string;
  notes?: string;
  isActive: boolean;
}) {
  return apiRequest<{ ruleId: string }>("/equipment/api/v1/maintenance/rules", {
    method: "POST",
    token,
    body
  });
}

export function getMaintenanceRecords(token: string) {
  return apiRequest<MaintenanceRecord[]>("/equipment/api/v1/maintenance/records", { token });
}

export function createMaintenanceRecord(token: string, body: {
  equipmentId: string;
  serviceDateUtc: string;
  description: string;
  cost?: number | null;
  performedBy?: string;
  counterpartyName?: string;
  serviceCategory: number;
  conditionAfterService: number;
}) {
  return apiRequest<{ recordId: string }>("/equipment/api/v1/maintenance/records", {
    method: "POST",
    token,
    body
  });
}

export function getMaintenanceAlerts(token: string) {
  return apiRequest<MaintenanceAlert[]>("/equipment/api/v1/maintenance/alerts", { token });
}

export function getMaintenanceSummary(token: string, filters?: { fromUtc?: string; toUtc?: string }) {
  return apiRequest<MaintenanceSummary>(withQuery("/equipment/api/v1/maintenance/summary", filters), { token });
}

export function getLessonEquipment(token: string, lessonId: string) {
  return apiRequest<LessonEquipmentState>(`/equipment/api/v1/lesson-equipment/${lessonId}`, {
    token
  });
}

export function reserveLessonEquipment(token: string, lessonId: string, body: {
  equipmentIds?: string[];
  kitIds?: string[];
  notes?: string;
}) {
  return apiRequest<{ reservationId: string; reservedItems: number }>(`/equipment/api/v1/lesson-equipment/${lessonId}/reserve`, {
    method: "POST",
    token,
    body
  });
}

export function releaseLessonEquipmentReservation(token: string, lessonId: string) {
  return apiRequest<{ released: boolean }>(`/equipment/api/v1/lesson-equipment/${lessonId}/reservation/release`, {
    method: "POST",
    token
  });
}

export function checkoutLessonEquipment(token: string, lessonId: string, body: {
  notesBefore?: string;
  items: Array<{
    equipmentId: string;
    conditionBefore: number;
    notesBefore?: string;
  }>;
}) {
  return apiRequest<{ checkoutId: string }>(`/equipment/api/v1/lesson-equipment/${lessonId}/checkout`, {
    method: "POST",
    token,
    body
  });
}

export function checkinLessonEquipment(token: string, lessonId: string, body: {
  notesAfter?: string;
  items: Array<{
    equipmentId: string;
    conditionAfter: number;
    notesAfter?: string;
  }>;
}) {
  return apiRequest<void>(`/equipment/api/v1/lesson-equipment/${lessonId}/checkin`, {
    method: "POST",
    token,
    body
  });
}

export function getDashboardReport(token: string, filters?: { fromUtc?: string; toUtc?: string }) {
  return apiRequest<DashboardReport>(withQuery("/reporting/api/v1/reports/dashboard", filters), { token });
}

export function getFinancialReport(token: string) {
  return apiRequest<FinancialReport>("/reporting/api/v1/reports/financial", { token });
}

export function getFinanceOverview(token: string, filters?: { fromUtc?: string; toUtc?: string }) {
  return apiRequest<FinanceOverview>(withQuery("/finance/api/v1/finance/overview", filters), { token });
}

export function getStudentFinancialStatuses(token: string) {
  return apiRequest<StudentFinancialStatusesResponse>("/finance/api/v1/finance/students/financial-statuses", { token });
}

export function getReceivableEntries(
  token: string,
  filters?: { fromDueUtc?: string; toDueUtc?: string; studentId?: string; includeSettled?: boolean }
) {
  return apiRequest<ReceivableEntry[]>(withQuery("/finance/api/v1/finance/receivables", filters), { token });
}

export function createReceivableEntry(token: string, body: {
  studentId: string;
  studentNameSnapshot: string;
  enrollmentId?: string | null;
  amount: number;
  dueAtUtc: string;
  description: string;
  notes?: string;
}) {
  return apiRequest<{ receivableId: string }>("/finance/api/v1/finance/receivables", {
    method: "POST",
    token,
    body
  });
}

export function updateReceivableEntry(token: string, receivableId: string, body: {
  studentId: string;
  studentNameSnapshot: string;
  enrollmentId?: string | null;
  amount: number;
  dueAtUtc: string;
  description: string;
  notes?: string;
}) {
  return apiRequest<void>(`/finance/api/v1/finance/receivables/${receivableId}`, {
    method: "PUT",
    token,
    body
  });
}

export function registerReceivablePayment(token: string, receivableId: string, body: {
  amount: number;
  paidAtUtc: string;
  note?: string;
}) {
  return apiRequest<{
    paymentId: string;
    receivableId: string;
    paidAmount: number;
    remainingAmount: number;
    status: string;
  }>(`/finance/api/v1/finance/receivables/${receivableId}/payments`, {
    method: "POST",
    token,
    body
  });
}

export function deleteReceivableEntry(token: string, receivableId: string) {
  return apiRequest<void>(`/finance/api/v1/finance/receivables/${receivableId}`, {
    method: "DELETE",
    token
  });
}

export function getRevenueEntries(token: string, filters?: { fromUtc?: string; toUtc?: string }) {
  return apiRequest<RevenueEntry[]>(withQuery("/finance/api/v1/finance/revenues", filters), { token });
}

export function createRevenueEntry(token: string, body: {
  sourceType: number;
  sourceId?: string | null;
  category: string;
  amount: number;
  recognizedAtUtc: string;
  description: string;
}) {
  return apiRequest<{ revenueId: string }>("/finance/api/v1/finance/revenues", {
    method: "POST",
    token,
    body
  });
}

export function updateRevenueEntry(token: string, revenueId: string, body: {
  sourceType: number;
  sourceId?: string | null;
  category: string;
  amount: number;
  recognizedAtUtc: string;
  description: string;
}) {
  return apiRequest<void>(`/finance/api/v1/finance/revenues/${revenueId}`, {
    method: "PUT",
    token,
    body
  });
}

export function deleteRevenueEntry(token: string, revenueId: string) {
  return apiRequest<void>(`/finance/api/v1/finance/revenues/${revenueId}`, {
    method: "DELETE",
    token
  });
}

export function getExpenseEntries(token: string, filters?: { fromUtc?: string; toUtc?: string }) {
  return apiRequest<ExpenseEntry[]>(withQuery("/finance/api/v1/finance/expenses", filters), { token });
}

export function createExpenseEntry(token: string, body: {
  category: number;
  amount: number;
  occurredAtUtc: string;
  description: string;
  vendor?: string;
}) {
  return apiRequest<{ expenseId: string }>("/finance/api/v1/finance/expenses", {
    method: "POST",
    token,
    body
  });
}

export function updateExpenseEntry(token: string, expenseId: string, body: {
  category: number;
  amount: number;
  occurredAtUtc: string;
  description: string;
  vendor?: string;
}) {
  return apiRequest<void>(`/finance/api/v1/finance/expenses/${expenseId}`, {
    method: "PUT",
    token,
    body
  });
}

export function deleteExpenseEntry(token: string, expenseId: string) {
  return apiRequest<void>(`/finance/api/v1/finance/expenses/${expenseId}`, {
    method: "DELETE",
    token
  });
}

export function getSchoolUsers(token: string) {
  return apiRequest<SchoolUser[]>("/api/v1/school-users", { token });
}

export function createSchoolUser(token: string, body: {
  fullName: string;
  email: string;
  password: string;
  role: number;
  permissions?: string[];
  phone?: string;
  avatarUrl?: string;
  isActive: boolean;
  mustChangePassword: boolean;
}) {
  return apiRequest<{
    profileId: string;
    identityUserId: string;
  }>("/api/v1/school-users", {
    method: "POST",
    token,
    body
  });
}

export function updateSchoolUser(token: string, identityUserId: string, body: {
  profileId: string;
  fullName: string;
  role: number;
  permissions?: string[];
  phone?: string;
  avatarUrl?: string;
  isActive: boolean;
  mustChangePassword: boolean;
}) {
  return apiRequest<void>(`/api/v1/school-users/${identityUserId}`, {
    method: "PUT",
    token,
    body
  });
}

export function getSchoolInvitations(token: string) {
  return apiRequest<SchoolInvitation[]>("/api/v1/school-users/invitations", { token });
}

export function createSchoolInvitation(token: string, body: {
  email: string;
  fullName: string;
  role: number;
  phone?: string;
  expiresInDays: number;
  schoolDisplayName?: string;
  schoolSlug?: string;
}) {
  return apiRequest<SchoolInvitation>("/api/v1/school-users/invitations", {
    method: "POST",
    token,
    body
  });
}

export function cancelSchoolInvitation(token: string, invitationId: string) {
  return apiRequest<void>(`/api/v1/school-users/invitations/${invitationId}/cancel`, {
    method: "POST",
    token
  });
}

export function resetSchoolUserPassword(token: string, identityUserId: string, body: {
  temporaryPassword: string;
  mustChangePassword: boolean;
  deliverByEmail?: boolean;
}) {
  return apiRequest<{
    resetAtUtc: string;
    userId: string;
    email: string;
    mustChangePassword: boolean;
    deliveryMode?: string;
    outboxFilePath?: string;
  }>(
    `/api/v1/school-users/${identityUserId}/reset-password`,
    {
      method: "POST",
      token,
      body
    }
  );
}

function withQuery(path: string, filters?: Record<string, string | undefined>) {
  if (!filters) {
    return path;
  }

  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(filters)) {
    if (value) {
      query.set(key, value);
    }
  }

  const queryString = query.toString();
  return queryString ? `${path}?${queryString}` : path;
}
