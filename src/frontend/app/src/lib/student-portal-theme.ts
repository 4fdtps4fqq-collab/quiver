import type { SessionSchool } from "../auth/SessionContext";

const defaultPrimary = "#0B3C5D";
const defaultAccent = "#2ED4A7";
const defaultSecondary = "#1FB6C9";

export type StudentPortalTheme = {
  primary: string;
  accent: string;
  primarySoft: string;
  accentSoft: string;
  frame: string;
  heroBackground: string;
  shellBackground: string;
  cardBackground: string;
  mutedCardBackground: string;
};

function normalizeHex(color: string | undefined, fallback: string) {
  if (!color) {
    return fallback;
  }

  const value = color.trim();
  return /^#([0-9a-fA-F]{6})$/.test(value) ? value : fallback;
}

function hexToRgb(hex: string) {
  const sanitized = hex.replace("#", "");
  return {
    r: Number.parseInt(sanitized.slice(0, 2), 16),
    g: Number.parseInt(sanitized.slice(2, 4), 16),
    b: Number.parseInt(sanitized.slice(4, 6), 16)
  };
}

function alpha(hex: string, opacity: number) {
  const { r, g, b } = hexToRgb(hex);
  return `rgba(${r}, ${g}, ${b}, ${opacity})`;
}

export function resolveStudentPortalTheme(school: SessionSchool | null): StudentPortalTheme {
  const primary = normalizeHex(school?.settings?.themePrimary, defaultPrimary);
  const accent = normalizeHex(school?.settings?.themeAccent, defaultAccent);
  const secondary = defaultSecondary;

  return {
    primary,
    accent,
    primarySoft: alpha(primary, 0.12),
    accentSoft: alpha(accent, 0.18),
    frame: alpha(primary, 0.18),
    heroBackground: `linear-gradient(135deg, ${alpha(accent, 0.16)}, ${alpha(secondary, 0.18)}, rgba(244,249,253,0.94))`,
    shellBackground: `linear-gradient(180deg, ${alpha(primary, 0.96)} 0%, ${alpha(primary, 0.84)} 28%, rgba(234,243,249,1) 100%)`,
    cardBackground: `linear-gradient(180deg, rgba(247,251,254,0.92), ${alpha(primary, 0.09)})`,
    mutedCardBackground: `linear-gradient(180deg, rgba(241,247,251,0.9), ${alpha(accent, 0.14)})`
  };
}
