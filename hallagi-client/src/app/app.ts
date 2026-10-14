import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Toasts } from './shared/ui';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Toasts],
  template: '<router-outlet /><app-toasts />',
  styleUrl: './app.scss'
})
export class App {}
