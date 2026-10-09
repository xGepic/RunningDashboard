import { Routes } from '@angular/router';
import { RunDetailPage } from './runs/run-detail-page';
import { RunsPage } from './runs/runs-page';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'runs' },
  { path: 'runs', component: RunsPage, title: 'Runs · Running Dashboard' },
  { path: 'runs/:id', component: RunDetailPage, title: 'Run · Running Dashboard' },
];
