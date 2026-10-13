import { Routes } from '@angular/router';
import { calendarRoutes } from './calendar/calendar-routes';
import { TrainingPlanPage } from './goal-races/training-plan-page';
import { OverviewPage } from './overview/overview-page';
import { RecordsPage } from './records/records-page';
import { RidesPage } from './rides/rides-page';
import { RunsPage } from './runs/runs-page';
import { SettingsPage } from './settings/settings-page';
import { ShoesPage } from './shoes/shoes-page';
import { TrainingPage } from './training/training-page';

export const routes: Routes = [
  { path: '', component: OverviewPage, title: 'Overview · Running Dashboard' },
  // Goal Races live on the Training page now; old links still lead there.
  { path: 'goal-races', redirectTo: 'training' },
  { path: 'goal-races/:id/training-plan', redirectTo: 'training/plan/:id' },
  { path: 'records', component: RecordsPage, title: 'Records · Running Dashboard' },
  { path: 'runs', component: RunsPage, title: 'Runs · Running Dashboard' },
  // Loaded when first opened: the Run's map brings in Leaflet.
  { path: 'runs/:id', loadComponent: () => import('./runs/run-detail-page').then((m) => m.RunDetailPage), title: 'Run · Running Dashboard' },
  // Ride and Swim history, opened from the Runs | Rides | Swims switch; they belong to the Runs section.
  { path: 'rides', component: RidesPage, title: 'Rides · Running Dashboard' },
  // Loaded when first opened, to keep the first load small.
  { path: 'swims', loadComponent: () => import('./swims/swims-page').then((m) => m.SwimsPage), title: 'Swims · Running Dashboard' },
  ...calendarRoutes,
  { path: 'shoes', component: ShoesPage, title: 'Shoes · Running Dashboard' },
  { path: 'settings', component: SettingsPage, title: 'Settings · Running Dashboard' },
  { path: 'training', component: TrainingPage, title: 'Training · Running Dashboard' },
  { path: 'training/plan/:id', component: TrainingPlanPage, title: 'Training Plan · Running Dashboard' },
];
