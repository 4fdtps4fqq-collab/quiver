import { apiRequest } from "./api";
import type { Role } from "../auth/SessionContext";

export type LoginPayload = {
  email: string;
  password: string;
  deviceName?: string;
};

export type LoginResponse = {
  token: string;
  accessTokenExpiresAtUtc: string;
  userId: string;
  schoolId: string | null;
  email: string;
  role: Role;
  permissions: string[];
  mustChangePassword: boolean;
};

export type IdentityMeResponse = {
  userId: string;
  schoolId: string | null;
  email: string;
  role: Role;
  permissions: string[];
  mustChangePassword: boolean;
};

export type InvitationPreviewResponse = {
  id: string;
  email: string;
  fullName: string;
  phone?: string;
  role: Role;
  expiresAtUtc: string;
  status: string;
};

export type InvitationAcceptanceResponse = {
  invitationAccepted: boolean;
  session: LoginResponse;
};

export type ForgotPasswordResponse = {
  requestedAtUtc: string;
  message: string;
};

export type SchoolInvitationDeliveryResponse = {
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

export type AuthenticationAuditEvent = {
  id: string;
  schoolId?: string;
  userAccountId?: string;
  targetUserAccountId?: string;
  eventType: string;
  outcome: string;
  email?: string;
  ipAddress?: string;
  userAgent?: string;
  metadata?: unknown;
  createdAtUtc: string;
};

export type SchoolCurrentResponse = {
  id: string;
  legalName: string;
  displayName: string;
  slug: string;
  logoDataUrl?: string;
  status: string;
  timezone: string;
  currencyCode: string;
  settings?: {
    id: string;
    schoolId: string;
    themePrimary: string;
    themeAccent: string;
    bookingLeadTimeMinutes: number;
    cancellationWindowHours: number;
    rescheduleWindowHours: number;
    attendanceConfirmationLeadMinutes: number;
    lessonReminderLeadHours: number;
    portalNotificationsEnabled: boolean;
    instructorBufferMinutes: number;
    noShowGraceMinutes: number;
    noShowConsumesCourseMinutes: boolean;
    noShowChargesSingleLesson: boolean;
    autoCreateEnrollmentRevenue: boolean;
    autoCreateSingleLessonRevenue: boolean;
    createdAtUtc: string;
  };
  users?: Array<{
    id: string;
    identityUserId: string;
    fullName: string;
    phone?: string;
    isActive: boolean;
  }>;
};

export type SchoolMeResponse = {
  id: string;
  identityUserId: string;
  fullName: string;
  phone?: string;
  avatarUrl?: string;
  isActive: boolean;
};

export function loginRequest(payload: LoginPayload) {
  return apiRequest<LoginResponse>("/identity/api/v1/auth/login", {
    method: "POST",
    body: payload
  });
}

export function refreshSessionRequest(payload?: { deviceName?: string }) {
  return apiRequest<LoginResponse>("/identity/api/v1/auth/refresh", {
    method: "POST",
    body: payload
  });
}

export function logoutSessionRequest() {
  return apiRequest<{ loggedOutAtUtc: string }>("/identity/api/v1/auth/logout", {
    method: "POST",
    body: {}
  });
}

export function getIdentityMe(token: string) {
  return apiRequest<IdentityMeResponse>("/identity/api/v1/auth/me", {
    token
  });
}

export function getSchoolCurrent(token: string) {
  return apiRequest<SchoolCurrentResponse>("/schools/api/v1/schools/current", {
    token
  });
}

export function getSchoolMe(token: string) {
  return apiRequest<SchoolMeResponse>("/schools/api/v1/schools/me", {
    token
  });
}

export function updateSchoolSettings(
  token: string,
  payload: {
    bookingLeadTimeMinutes: number;
    cancellationWindowHours: number;
    rescheduleWindowHours: number;
    attendanceConfirmationLeadMinutes: number;
    lessonReminderLeadHours: number;
    portalNotificationsEnabled: boolean;
    instructorBufferMinutes: number;
    noShowGraceMinutes: number;
    noShowConsumesCourseMinutes: boolean;
    noShowChargesSingleLesson: boolean;
    autoCreateEnrollmentRevenue: boolean;
    autoCreateSingleLessonRevenue: boolean;
    themePrimary?: string;
    themeAccent?: string;
  }
) {
  return apiRequest<{
    updatedAtUtc: string;
    bookingLeadTimeMinutes: number;
    cancellationWindowHours: number;
    rescheduleWindowHours: number;
    attendanceConfirmationLeadMinutes: number;
    lessonReminderLeadHours: number;
    portalNotificationsEnabled: boolean;
    instructorBufferMinutes: number;
    noShowGraceMinutes: number;
    noShowConsumesCourseMinutes: boolean;
    noShowChargesSingleLesson: boolean;
    autoCreateEnrollmentRevenue: boolean;
    autoCreateSingleLessonRevenue: boolean;
    themePrimary: string;
    themeAccent: string;
  }>("/schools/api/v1/schools/settings", {
    method: "PUT",
    token,
    body: payload
  });
}

export function previewInvitation(token: string) {
  return apiRequest<InvitationPreviewResponse>(`/api/v1/school-users/invitations/preview?token=${encodeURIComponent(token)}`);
}

export function acceptInvitation(payload: { token: string; password: string }) {
  return apiRequest<InvitationAcceptanceResponse>("/api/v1/school-users/invitations/accept", {
    method: "POST",
    body: payload
  });
}

export function changePasswordRequest(
  token: string,
  payload: { currentPassword: string; newPassword: string; deviceName?: string }
) {
  return apiRequest<LoginResponse>("/identity/api/v1/auth/change-password", {
    method: "POST",
    token,
    body: payload
  });
}

export function forgotPasswordRequest(payload: { email: string }) {
  return apiRequest<ForgotPasswordResponse>("/identity/api/v1/auth/forgot-password", {
    method: "POST",
    body: payload
  });
}

export function resetPasswordRequest(payload: { token: string; newPassword: string; deviceName?: string }) {
  return apiRequest<LoginResponse>("/identity/api/v1/auth/reset-password", {
    method: "POST",
    body: payload
  });
}

export function getAuthenticationAuditEvents(token: string, take = 40) {
  return apiRequest<AuthenticationAuditEvent[]>(`/identity/api/v1/audit-events?take=${take}`, {
    token
  });
}
