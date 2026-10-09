import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ConnectionStatus } from './connection/connection-status';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ConnectionStatus],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {}
