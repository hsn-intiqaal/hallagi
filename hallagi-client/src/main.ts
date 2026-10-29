import { bootstrapApplication } from '@angular/platform-browser';
import { createAppConfig } from './app/app.config';
import { App } from './app/app';
import { loadAppConfig } from './app/core/config/app-config';

// Deployment config first (API address, brand), then the application.
loadAppConfig()
  .then(config => {
    document.title = `${config.brand.name} — ${config.brand.tagline}`;
    return bootstrapApplication(App, createAppConfig(config));
  })
  .catch(err => console.error(err));
