import { AppRouter } from "./router";
import { SessionProvider } from "../auth/SessionContext";

export function App() {
  return (
    <SessionProvider>
      <AppRouter />
    </SessionProvider>
  );
}
