import { Routes } from '@angular/router';
import { RunsPage } from './runs/runs-page';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'runs' },
  { path: 'runs', component: RunsPage, title: 'Runs · Running Dashboard' },
];
