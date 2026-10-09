import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ConnectionStatus } from './connection/connection-status';

@Component({
  imports: [RouterOutlet, ConnectionStatus],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {}
