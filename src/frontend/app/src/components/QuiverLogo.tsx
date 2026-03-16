type QuiverLogoProps = {
  layout?: "horizontal" | "stacked" | "mark";
  size?: "sm" | "md" | "lg";
  showTagline?: boolean;
  className?: string;
  textClassName?: string;
  light?: boolean;
};

const sizeMap = {
  sm: {
    mark: 54,
    wordmark: "text-[2.05rem]",
    tagline: "text-[10px]"
  },
  md: {
    mark: 82,
    wordmark: "text-[3.7rem]",
    tagline: "text-[12px]"
  },
  lg: {
    mark: 116,
    wordmark: "text-[5.4rem]",
    tagline: "text-[14px]"
  }
} as const;

function joinClasses(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

export function QuiverLogo({
  layout = "horizontal",
  size = "md",
  showTagline = true,
  className,
  textClassName,
  light = false
}: QuiverLogoProps) {
  const config = sizeMap[size];
  const toneClass = light ? "text-white" : "text-[var(--q-brand-wordmark)]";
  const subtitleClass = light ? "text-white/80" : "text-[var(--q-brand-secondary)]";

  if (layout === "mark") {
    return (
      <div className={className}>
        <QuiverMark size={config.mark} />
      </div>
    );
  }

  if (layout === "stacked") {
    return (
      <div className={joinClasses("inline-flex flex-col items-center gap-2", className)}>
        <QuiverMark size={config.mark} />
        <div className="text-center">
          <div
            className={joinClasses("leading-none tracking-[-0.045em]", config.wordmark, toneClass, textClassName)}
            style={{ fontWeight: 600 }}
          >
            Quiver
          </div>
          {showTagline ? (
            <div className={joinClasses("mt-3 uppercase tracking-[0.5em]", config.tagline, subtitleClass)}>
              Kite Experience
            </div>
          ) : null}
        </div>
      </div>
    );
  }

  return (
    <div className={joinClasses("inline-flex items-center gap-3", className)}>
      <QuiverMark size={config.mark} />
      <div>
        <div
          className={joinClasses("leading-none tracking-[-0.04em]", config.wordmark, toneClass, textClassName)}
          style={{ fontWeight: 600 }}
        >
          Quiver
        </div>
        {showTagline ? (
          <div className={joinClasses("mt-1.5 uppercase tracking-[0.42em]", config.tagline, subtitleClass)}>
            Kite Experience
          </div>
        ) : null}
      </div>
    </div>
  );
}

function QuiverMark({ size }: { size: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 180 130"
      fill="none"
      role="img"
      aria-label="Quiver logo"
    >
      <defs>
        <linearGradient id="quiverSphere" x1="40" y1="108" x2="126" y2="24" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor="var(--q-brand-primary-soft)" />
          <stop offset="0.45" stopColor="var(--q-brand-primary)" />
          <stop offset="0.72" stopColor="var(--q-brand-secondary)" />
          <stop offset="1" stopColor="var(--q-brand-wordmark)" />
        </linearGradient>
        <linearGradient id="quiverWave" x1="55" y1="104" x2="130" y2="83" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor="var(--q-brand-primary)" />
          <stop offset="1" stopColor="var(--q-brand-secondary)" />
        </linearGradient>
      </defs>

      <path
        d="M42 92C34 63 47 37 76 22C103 9 134 14 152 29C131 28 109 36 88 55C72 68 58 81 42 92Z"
        fill="url(#quiverSphere)"
      />
      <path
        d="M47 101C66 68 95 41 127 25C149 14 168 14 174 26C180 39 173 59 154 79C140 94 124 106 103 114C119 95 126 79 123 61C119 42 105 32 83 31C68 30 56 34 42 36C42 58 44 80 47 101Z"
        fill="url(#quiverSphere)"
      />
      <path
        d="M56 39C76 37 94 39 107 44C88 42 71 43 52 45L56 39Z"
        fill="white"
        opacity="0.9"
      />
      <path
        d="M118 27C138 20 155 20 165 28C176 38 174 55 163 72C149 93 131 109 103 117C121 96 131 77 136 60C142 41 136 31 118 27Z"
        fill="white"
      />
      <path
        d="M63 108C80 96 98 87 117 83C100 85 83 92 67 103C84 97 103 95 122 100C107 92 89 91 70 96C67 100 65 104 63 108Z"
        fill="url(#quiverWave)"
      />
      <path
        d="M119 83C132 84 143 90 151 99C141 95 132 94 122 95C131 99 141 99 153 96C145 106 131 113 113 115C122 107 125 97 119 83Z"
        fill="url(#quiverWave)"
      />
      <path
        d="M34 95C63 57 93 31 129 18C147 11 161 11 170 15C147 12 119 22 90 43C66 60 48 78 34 95Z"
        stroke="white"
        strokeWidth="9"
        strokeLinecap="round"
      />
      <path
        d="M37 97C70 57 104 31 142 22C156 19 167 20 173 27"
        stroke="var(--q-brand-wordmark)"
        strokeWidth="5.5"
        strokeLinecap="round"
      />
    </svg>
  );
}
