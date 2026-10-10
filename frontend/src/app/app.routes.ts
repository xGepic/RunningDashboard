import { Routes } from '@angular/router';
import { GoalRacesPage } from './goal-races/goal-races-page';
import { OverviewPage } from './overview/overview-page';
import { RecordsPage } from './records/records-page';
import { RunDetailPage } from './runs/run-detail-page';
import { RunsPage } from './runs/runs-page';
import { ShoesPage } from './shoes/shoes-page';
import { TrainingPage } from './training/training-page';

export const routes: Routes = [
  { path: '', component: OverviewPage, title: 'Overview · Running Dashboard' },
  { path: 'goal-races', component: GoalRacesPage, title: 'Goal Races · Running Dashboard' },
  { path: 'records', component: RecordsPage, title: 'Records · Running Dashboard' },
  { path: 'runs', component: RunsPage, title: 'Runs · Running Dashboard' },
  { path: 'runs/:id', component: RunDetailPage, title: 'Run · Running Dashboard' },
  { path: 'shoes', component: ShoesPage, title: 'Shoes · Running Dashboard' },
  { path: 'training', component: TrainingPage, title: 'Training · Running Dashboard' },
];
