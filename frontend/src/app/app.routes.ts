import { Routes } from '@angular/router';
import { calendarRoutes } from './calendar/calendar-routes';
import { RaceCalculatorPage } from './calculator/race-calculator-page';
import { TrainingPlanPage } from './goal-races/training-plan-page';
import { FitnessPage } from './fitness/fitness-page';
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
  { path: 'fitness', component: FitnessPage, title: 'Fitness · Running Dashboard' },
  { path: 'runs', component: RunsPage, title: 'Runs · Running Dashboard' },
  // Loaded when first opened: the Run's map brings in Leaflet.
  { path: 'runs/:id', loadComponent: () => import('./runs/run-detail-page').then((m) => m.RunDetailPage), title: 'Run · Running Dashboard' },
  // Ride and Swim history, opened from the Runs | Rides | Swims switch; they belong to the Runs section.
  { path: 'rides', component: RidesPage, title: 'Rides · Running Dashboard' },
  // Loaded when first opened: the Ride's map brings in Leaflet.
  { path: 'rides/:id', loadComponent: () => import('./rides/ride-detail-page').then((m) => m.RideDetailPage), title: 'Ride · Running Dashboard' },
  // Loaded when first opened, to keep the first load small.
  { path: 'swims', loadComponent: () => import('./swims/swims-page').then((m) => m.SwimsPage), title: 'Swims · Running Dashboard' },
  { path: 'swims/:id', loadComponent: () => import('./swims/swim-detail-page').then((m) => m.SwimDetailPage), title: 'Swim · Running Dashboard' },
  ...calendarRoutes,
  { path: 'shoes', component: ShoesPage, title: 'Shoes · Running Dashboard' },
  { path: 'calculator', component: RaceCalculatorPage, title: 'Race Calculator · Running Dashboard' },
  { path: 'settings', component: SettingsPage, title: 'Settings · Running Dashboard' },
  { path: 'training', component: TrainingPage, title: 'Training · Running Dashboard' },
  { path: 'training/plan/:id', component: TrainingPlanPage, title: 'Training Plan · Running Dashboard' },
];
