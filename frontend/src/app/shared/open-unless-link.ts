import { NavigationExtras, Router } from '@angular/router';

/** For a whole row or card that opens a page: goes there, unless the click was on a link inside it, which goes where it points. */
export function openUnlessLink(event: MouseEvent, router: Router, commands: unknown[], extras?: NavigationExtras): void {
  if ((event.target as HTMLElement).closest('a')) return;
  router.navigate(commands, extras);
}
