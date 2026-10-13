import { HttpErrorResponse } from '@angular/common/http';

/** Field errors as the API reports them in its problem details, keyed by field name. */
export type FieldErrors = Record<string, string[]>;

/** Where errors that belong to no single field go. */
export const formError = 'form';

/** The field errors the API reported for a failed save; without any (e.g. the API was down), `fallback` for the form. */
export function fieldErrorsOf(response: HttpErrorResponse, fallback: string): FieldErrors {
  return response.error?.errors ?? { [formError]: [fallback] };
}
