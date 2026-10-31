import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import './styles.css';
import { applyTheme, cachedTheme, defaultTheme } from './theme/themes';

// Show the last used theme straight away; the signed-in account's saved theme is applied once it loads.
applyTheme(cachedTheme() ?? defaultTheme);

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
