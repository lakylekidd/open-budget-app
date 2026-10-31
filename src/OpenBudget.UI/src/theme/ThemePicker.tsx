import { themeSwatches, themes, type ThemeKey } from './themes';
import './themes.css';

type Props = { value: ThemeKey; disabled?: boolean; onChange: (key: ThemeKey) => void };

/** Radio group of theme cards. Arrow keys move the selection, as with any native radio group. */
export function ThemePicker({ value, disabled, onChange }: Props) {
  return <div className="theme-picker" role="radiogroup" aria-label="Theme">
    {themes.map(theme => {
      const swatch = themeSwatches(theme.key);
      return <label key={theme.key} className={`theme-option ${value === theme.key ? 'is-selected' : ''}`}>
        <input type="radio" name="theme" value={theme.key} checked={value === theme.key} disabled={disabled} onChange={() => onChange(theme.key)} />
        <span className="theme-swatch" aria-hidden="true" style={{ background: swatch.background }}>
          <span style={{ background: swatch.surface }} /><span style={{ background: swatch.accent }} />
        </span>
        <span className="theme-label"><strong>{theme.label}</strong><small>{theme.description}</small></span>
      </label>;
    })}
  </div>;
}
