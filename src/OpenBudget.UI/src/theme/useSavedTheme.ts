import { useEffect } from 'react';
import { applyTheme, loadThemePreference } from './themes';

/** Applies the signed-in account's saved theme once the app is shown. If it cannot be loaded the current theme is kept. */
export function useSavedTheme() {
  useEffect(() => {
    const controller = new AbortController();
    void loadThemePreference(controller.signal).then(theme => { if (theme && !controller.signal.aborted) applyTheme(theme); });
    return () => controller.abort();
  }, []);
}
