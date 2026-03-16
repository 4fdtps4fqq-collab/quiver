import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode
} from "react";
import {
  acceptInvitation,
  changePasswordRequest,
  getIdentityMe,
  getSchoolCurrent,
  getSchoolMe,
  loginRequest,
  logoutSessionRequest,
  previewInvitation,
  refreshSessionRequest,
  type LoginPayload
} from "../lib/auth-api";

export type Role = "SystemAdmin" | "Owner" | "Admin" | "Instructor" | "Student";

export type SessionSchoolSettings = {
  themePrimary: string;
  themeAccent: string;
  bookingLeadTimeMinutes: number;
  cancellationWindowHours: number;
  rescheduleWindowHours: number;
  attendanceConfirmationLeadMinutes: number;
  lessonReminderLeadHours: number;
  portalNotificationsEnabled: boolean;
};

export type SessionSchool = {
  id: string;
  displayName: string;
  slug: string;
  logoDataUrl?: string;
  timezone: string;
  currencyCode: string;
  settings: SessionSchoolSettings | null;
};

export type SessionUser = {
  id: string;
  fullName: string;
  email: string;
  role: Role;
  permissions: string[];
  schoolName: string;
  mustChangePassword: boolean;
};

type StoredSession = {
  token: string;
  refreshToken: string | null;
  refreshTokenExpiresAtUtc: string | null;
  user: SessionUser;
  school: SessionSchool | null;
};

type SessionContextValue = {
  user: SessionUser | null;
  school: SessionSchool | null;
  token: string | null;
  isLoading: boolean;
  login: (payload: LoginPayload) => Promise<SessionUser>;
  acceptInvite: (payload: { token: string; password: string }) => Promise<SessionUser>;
  previewInvite: (token: string) => ReturnType<typeof previewInvitation>;
  changePassword: (payload: { currentPassword: string; newPassword: string }) => Promise<void>;
  logout: () => Promise<void>;
};

const SessionContext = createContext<SessionContextValue | null>(null);
const storageKey = "kiteflow-platform-session";
const defaultSchoolName = "Quiver School";
const defaultSystemAdminArea = "Administração da plataforma";

export function SessionProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<SessionUser | null>(null);
  const [school, setSchool] = useState<SessionSchool | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState<string | null>(null);
  const [refreshTokenExpiresAtUtc, setRefreshTokenExpiresAtUtc] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const raw = window.localStorage.getItem(storageKey);
    if (!raw) {
      setIsLoading(false);
      return;
    }

    try {
      const stored = JSON.parse(raw) as StoredSession;
      void hydrate(stored.token, stored.refreshToken, stored.refreshTokenExpiresAtUtc);
    } catch {
      clearSession();
      setIsLoading(false);
    }
  }, []);

  const value = useMemo<SessionContextValue>(
    () => ({
      user,
      school,
      token,
      isLoading,
      login: async (payload) => {
        const session = await loginRequest({
          ...payload,
          deviceName: payload.deviceName ?? resolveDeviceName()
        });

        return hydrate(session.token, session.refreshToken, session.refreshTokenExpiresAtUtc);
      },
      acceptInvite: async (payload) => {
        const response = await acceptInvitation(payload);
        return hydrate(
          response.session.token,
          response.session.refreshToken,
          response.session.refreshTokenExpiresAtUtc
        );
      },
      previewInvite: (inviteToken) => previewInvitation(inviteToken),
      changePassword: async (payload) => {
        if (!token) {
          throw new Error("Sessão não encontrada.");
        }

        const session = await changePasswordRequest(token, {
          ...payload,
          deviceName: resolveDeviceName()
        });

        await hydrate(session.token, session.refreshToken, session.refreshTokenExpiresAtUtc);
      },
      logout: async () => {
        if (refreshToken) {
          try {
            await logoutSessionRequest({ refreshToken });
          } catch {
            // Ignoramos erros de logout remoto para garantir a limpeza local da sessão.
          }
        }

        clearSession();
      }
    }),
    [isLoading, refreshToken, refreshTokenExpiresAtUtc, school, token, user]
  );

  async function hydrate(
    nextToken: string | null,
    nextRefreshToken: string | null,
    nextRefreshTokenExpiresAtUtc: string | null
  ) {
    try {
      setIsLoading(true);

      if (!nextToken) {
        throw new Error("Sessão sem token de acesso.");
      }

      return await loadSession(nextToken, nextRefreshToken, nextRefreshTokenExpiresAtUtc);
    } catch {
      if (nextRefreshToken) {
        try {
          const refreshed = await refreshSessionRequest({
            refreshToken: nextRefreshToken,
            deviceName: resolveDeviceName()
          });

          return await loadSession(
            refreshed.token,
            refreshed.refreshToken,
            refreshed.refreshTokenExpiresAtUtc
          );
        } catch {
          clearSession();
          throw new Error("Não foi possível renovar sua sessão. Entre novamente.");
        }
      }

      clearSession();
      throw new Error("Não foi possível carregar a sessão.");
    } finally {
      setIsLoading(false);
    }
  }

  async function loadSession(
    nextToken: string,
    nextRefreshToken: string | null,
    nextRefreshTokenExpiresAtUtc: string | null
  ) {
    const identity = await getIdentityMe(nextToken);

    const shouldLoadSchoolContext =
      identity.role !== "SystemAdmin" && Boolean(identity.schoolId);

    const ignoredReason = new Error("ignored");
    const [school, profile] = shouldLoadSchoolContext
      ? await Promise.allSettled([getSchoolCurrent(nextToken), getSchoolMe(nextToken)])
      : ([
          {
            status: "rejected",
            reason: ignoredReason
          },
          {
            status: "rejected",
            reason: ignoredReason
          }
        ] as const);

    const schoolName =
      school.status === "fulfilled"
        ? school.value.displayName
        : identity.role === "SystemAdmin"
          ? defaultSystemAdminArea
          : defaultSchoolName;

    const nextSchool: SessionSchool | null =
      school.status === "fulfilled"
        ? {
            id: school.value.id,
            displayName: school.value.displayName,
            slug: school.value.slug,
            logoDataUrl: school.value.logoDataUrl,
            timezone: school.value.timezone,
            currencyCode: school.value.currencyCode,
            settings: school.value.settings
              ? {
                  themePrimary: school.value.settings.themePrimary,
                  themeAccent: school.value.settings.themeAccent,
                  bookingLeadTimeMinutes: school.value.settings.bookingLeadTimeMinutes,
                  cancellationWindowHours: school.value.settings.cancellationWindowHours,
                  rescheduleWindowHours: school.value.settings.rescheduleWindowHours,
                  attendanceConfirmationLeadMinutes: school.value.settings.attendanceConfirmationLeadMinutes,
                  lessonReminderLeadHours: school.value.settings.lessonReminderLeadHours,
                  portalNotificationsEnabled: school.value.settings.portalNotificationsEnabled
                }
              : null
          }
        : null;

    const fullName =
      profile.status === "fulfilled"
        ? profile.value.fullName
        : identity.role === "SystemAdmin"
          ? "Administrador do sistema"
          : identity.email.split("@")[0];

    const nextUser: SessionUser = {
      id: identity.userId,
      fullName,
      email: identity.email,
      role: identity.role,
      permissions: identity.permissions ?? [],
      schoolName,
      mustChangePassword: identity.mustChangePassword
    };

    window.localStorage.setItem(
      storageKey,
      JSON.stringify({
        token: nextToken,
        refreshToken: nextRefreshToken,
        refreshTokenExpiresAtUtc: nextRefreshTokenExpiresAtUtc,
        user: nextUser,
        school: nextSchool
      } satisfies StoredSession)
    );

    setToken(nextToken);
    setRefreshToken(nextRefreshToken);
    setRefreshTokenExpiresAtUtc(nextRefreshTokenExpiresAtUtc);
    setUser(nextUser);
    setSchool(nextSchool);
    return nextUser;
  }

  function clearSession() {
    window.localStorage.removeItem(storageKey);
    setToken(null);
    setRefreshToken(null);
    setRefreshTokenExpiresAtUtc(null);
    setUser(null);
    setSchool(null);
  }

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

function resolveDeviceName() {
  const platform = window.navigator.platform?.trim();
  if (platform) {
    return `Quiver Web - ${platform}`;
  }

  return "Quiver Web";
}

export function useSession() {
  const context = useContext(SessionContext);
  if (!context) {
    throw new Error("useSession precisa ser usado dentro de SessionProvider.");
  }

  return context;
}
