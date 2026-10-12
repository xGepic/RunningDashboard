import { Routes } from '@angular/router';
import { CalendarPage } from './calendar-page';

/** The current year at /calendar, others at /calendar/2025; a month adds its fragment, e.g. /calendar/2025#Dec. */
export const calendarRoutes: Routes = [
  { path: 'calendar', component: CalendarPage, title: 'Calendar · Running Dashboard' },
  { path: 'calendar/:year', component: CalendarPage, title: 'Calendar · Running Dashboard' },
];
