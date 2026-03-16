import { useEffect, useId, useRef, useState } from "react";

type Coordinates = {
  latitude?: number;
  longitude?: number;
};

type SchoolBaseMapPickerProps = {
  value: Coordinates;
  onChange: (value: Coordinates) => void;
};

const leafletCssId = "kiteflow-leaflet-css";
const leafletScriptId = "kiteflow-leaflet-script";
let leafletLoadPromise: Promise<void> | null = null;

export function SchoolBaseMapPicker({ value, onChange }: SchoolBaseMapPickerProps) {
  const containerId = useId().replace(/:/g, "-");
  const mapRef = useRef<any>(null);
  const [isReady, setIsReady] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    void ensureLeafletLoaded()
      .then(() => {
        if (!isMounted) {
          return;
        }

        const leaflet = getLeaflet();
        if (!leaflet) {
          setLoadError("Não foi possível carregar o mapa agora.");
          return;
        }

        const map = leaflet.map(containerId, {
          zoomControl: true,
          attributionControl: true
        }).setView(
          [value.latitude ?? -22.971, value.longitude ?? -43.182],
          value.latitude && value.longitude ? 15 : 5
        );

        leaflet
          .tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: "&copy; OpenStreetMap contributors"
          })
          .addTo(map);

        map.on("click", (event: { latlng: { lat: number; lng: number } }) => {
          const nextValue = {
            latitude: Number(event.latlng.lat.toFixed(6)),
            longitude: Number(event.latlng.lng.toFixed(6))
          };

          syncMarker(nextValue, map, leaflet);
          onChange(nextValue);
        });

        mapRef.current = map;
        setIsReady(true);

        if (value.latitude && value.longitude) {
          syncMarker(value, map, leaflet);
        }
      })
      .catch(() => {
        if (isMounted) {
          setLoadError("Não foi possível carregar o mapa agora.");
        }
      });

    return () => {
      isMounted = false;
      if (mapRef.current) {
        mapRef.current.remove();
        mapRef.current = null;
      }
    };
  }, [containerId, onChange]);

  useEffect(() => {
    const leaflet = getLeaflet();
    const map = mapRef.current;
    if (!leaflet || !map) {
      return;
    }

    if (value.latitude && value.longitude) {
      syncMarker(value, map, leaflet);
      map.setView([value.latitude, value.longitude], Math.max(map.getZoom(), 15));
      return;
    }

    if (markerRef.current) {
      map.removeLayer(markerRef.current);
      markerRef.current = null;
    }
  }, [value.latitude, value.longitude]);

  const hasSelection = typeof value.latitude === "number" && typeof value.longitude === "number";
  const googleMapsUrl = hasSelection
    ? `https://www.google.com/maps/search/?api=1&query=${value.latitude},${value.longitude}`
    : null;

  return (
    <div className="space-y-3">
      <div className="rounded-[28px] border border-[var(--q-border)] bg-[var(--q-surface)] p-3 shadow-[0_18px_36px_rgba(10,45,88,0.06)]">
        <div
          id={containerId}
          className="h-[320px] w-full overflow-hidden rounded-[22px] border border-[var(--q-divider)] bg-[linear-gradient(180deg,#e9f6ff_0%,#dff6f1_100%)]"
        />
      </div>

      {loadError ? (
        <div className="rounded-2xl border border-[var(--q-warning)]/40 bg-[var(--q-warning-bg)] px-4 py-3 text-sm text-[var(--q-text)]">
          {loadError}
        </div>
      ) : null}

      {!loadError ? (
        <div className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text-2)]">
          {isReady
            ? "Clique no mapa para marcar o ponto da praia base ou da escola."
            : "Carregando mapa para seleção da base da escola..."}
        </div>
      ) : null}

      <div className="grid gap-3 md:grid-cols-[1fr_1fr_auto_auto]">
        <div className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3">
          <div className="text-xs uppercase tracking-[0.18em] text-[var(--q-muted)]">Latitude</div>
          <div className="mt-1 text-sm font-medium text-[var(--q-text)]">
            {hasSelection ? value.latitude?.toFixed(6) : "Selecione no mapa"}
          </div>
        </div>
        <div className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3">
          <div className="text-xs uppercase tracking-[0.18em] text-[var(--q-muted)]">Longitude</div>
          <div className="mt-1 text-sm font-medium text-[var(--q-text)]">
            {hasSelection ? value.longitude?.toFixed(6) : "Selecione no mapa"}
          </div>
        </div>
        <button
          type="button"
          onClick={() => onChange({ latitude: undefined, longitude: undefined })}
          className="rounded-2xl border border-[var(--q-border)] px-4 py-3 text-sm font-medium text-[var(--q-text)] transition hover:bg-[var(--q-surface-2)]"
        >
          Limpar ponto
        </button>
        <a
          href={googleMapsUrl ?? undefined}
          target="_blank"
          rel="noreferrer"
          className={`rounded-2xl px-4 py-3 text-sm font-medium transition ${
            googleMapsUrl
              ? "border border-[var(--q-info)]/40 bg-[var(--q-info-bg)] text-[var(--q-text)] hover:opacity-90"
              : "pointer-events-none border border-[var(--q-border)] bg-[var(--q-surface-2)] text-[var(--q-muted)]"
          }`}
        >
          Abrir no Google Maps
        </a>
      </div>
    </div>
  );
}

function getLeaflet() {
  return (window as unknown as { L?: any }).L;
}

function syncMarker(value: Coordinates, map: any, leaflet: any) {
  if (typeof value.latitude !== "number" || typeof value.longitude !== "number") {
    return;
  }

  const marker = (map.__kiteflowMarker as any) ?? leaflet.circleMarker([value.latitude, value.longitude], {
    radius: 10,
    color: "#0B3C5D",
    weight: 3,
    fillColor: "#2ED4A7",
    fillOpacity: 0.92
  });

  marker.setLatLng([value.latitude, value.longitude]);

  if (!map.__kiteflowMarker) {
    marker.addTo(map);
    map.__kiteflowMarker = marker;
  }
}

function ensureLeafletLoaded() {
  if (getLeaflet()) {
    return Promise.resolve();
  }

  if (leafletLoadPromise) {
    return leafletLoadPromise;
  }

  leafletLoadPromise = new Promise<void>((resolve, reject) => {
    if (!document.getElementById(leafletCssId)) {
      const link = document.createElement("link");
      link.id = leafletCssId;
      link.rel = "stylesheet";
      link.href = "https://unpkg.com/leaflet@1.9.4/dist/leaflet.css";
      document.head.appendChild(link);
    }

    const existingScript = document.getElementById(leafletScriptId) as HTMLScriptElement | null;
    if (existingScript) {
      if (getLeaflet()) {
        resolve();
        return;
      }

      existingScript.addEventListener("load", () => resolve(), { once: true });
      existingScript.addEventListener("error", () => reject(new Error("leaflet-load-failed")), { once: true });
      return;
    }

    const script = document.createElement("script");
    script.id = leafletScriptId;
    script.src = "https://unpkg.com/leaflet@1.9.4/dist/leaflet.js";
    script.async = true;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error("leaflet-load-failed"));
    document.body.appendChild(script);
  });

  return leafletLoadPromise;
}
