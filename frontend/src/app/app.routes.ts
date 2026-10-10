import { Routes } from '@angular/router';
import { OverviewPage } from './overview/overview-page';
import { RecordsPage } from './records/records-page';
import { RunDetailPage } from './runs/run-detail-page';
import { RunsPage } from './runs/runs-page';
import { ShoesPage } from './shoes/shoes-page';

export const routes: Routes = [
  { path: '', component: OverviewPage, title: 'Overview · Running Dashboard' },
  { path: 'records', component: RecordsPage, title: 'Records · Running Dashboard' },
  { path: 'runs', component: RunsPage, title: 'Runs · Running Dashboard' },
  { path: 'runs/:id', component: RunDetailPage, title: 'Run · Running Dashboard' },
  { path: 'shoes', component: ShoesPage, title: 'Shoes · Running Dashboard' },
];
