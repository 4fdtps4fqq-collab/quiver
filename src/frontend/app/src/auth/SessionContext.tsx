import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useRef,
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
import { registerAuthRefreshHandler } from "../lib/api";

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
const defaultSchoolName = "Quiver School";
const defaultSystemAdminArea = "Administração da plataforma";
const clientIdleTimeoutMs = 30 * 60 * 1000;

export function SessionProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<SessionUser | null>(null);
  const [school, setSchool] = useState<SessionSchool | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const idleTimerRef = useRef<number | null>(null);
  const tokenRef = useRef<string | null>(null);

  useEffect(() => {
    void hydrateFromRefreshCookie();
  }, []);

  useEffect(() => {
    tokenRef.current = token;
  }, [token]);

  useEffect(() => {
    registerAuthRefreshHandler(async () => {
      try {
        const refreshed = await refreshSessionRequest({
          deviceName: resolveDeviceName()
        });

        await loadSession(refreshed.token);
        return refreshed.token;
      } catch {
        clearSession();
        return null;
      }
    });

    return () => registerAuthRefreshHandler(null);
  }, []);

  useEffect(() => {
    if (!token || !user) {
      clearIdleTimer();
      return;
    }

    const activityEvents: Array<keyof WindowEventMap> = ["mousemove", "keydown", "click", "scroll", "touchstart"];
    const resetIdleTimer = () => {
      clearIdleTimer();
      idleTimerRef.current = window.setTimeout(() => {
        void handleIdleTimeout();
      }, clientIdleTimeoutMs);
    };

    activityEvents.forEach((eventName) => window.addEventListener(eventName, resetIdleTimer, { passive: true }));
    resetIdleTimer();

    return () => {
      activityEvents.forEach((eventName) => window.removeEventListener(eventName, resetIdleTimer));
      clearIdleTimer();
    };
  }, [token, user]);

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

        return loadSession(session.token);
      },
      acceptInvite: async (payload) => {
        const response = await acceptInvitation(payload);
        const session = await loginRequest({
          email: response.session.email,
          password: payload.password,
          deviceName: resolveDeviceName()
        });

        return loadSession(session.token);
      },
      previewInvite: (inviteToken) => previewInvitation(inviteToken),
      changePassword: async (payload) => {
        if (!tokenRef.current) {
          throw new Error("Sessão não encontrada.");
        }

        const session = await changePasswordRequest(tokenRef.current, {
          ...payload,
          deviceName: resolveDeviceName()
        });

        await loadSession(session.token);
      },
      logout: async () => {
        try {
          await logoutSessionRequest();
        } catch {
          // A limpeza local segue sendo obrigatória.
        }

        clearSession();
      }
    }),
    [isLoading, school, token, user]
  );

  async function hydrateFromRefreshCookie() {
    try {
      setIsLoading(true);
      const refreshed = await refreshSessionRequest({
        deviceName: resolveDeviceName()
      });
      await loadSession(refreshed.token);
    } catch {
      clearSession();
    } finally {
      setIsLoading(false);
    }
  }

  async function loadSession(nextToken: string) {
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

    setToken(nextToken);
    setUser(nextUser);
    setSchool(nextSchool);
    return nextUser;
  }

  async function handleIdleTimeout() {
    if (!tokenRef.current) {
      return;
    }

    try {
      await logoutSessionRequest();
    } catch {
      // A limpeza local segue sendo obrigatória mesmo sem resposta remota.
    }

    clearSession();
  }

  function clearIdleTimer() {
    if (idleTimerRef.current !== null) {
      window.clearTimeout(idleTimerRef.current);
      idleTimerRef.current = null;
    }
  }

  function clearSession() {
    clearIdleTimer();
    setToken(null);
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
