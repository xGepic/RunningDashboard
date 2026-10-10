/** Field errors as the API reports them in its problem details, keyed by field name. */
export type FieldErrors = Record<string, string[]>;

/** Where errors that belong to no single field go. */
export const formError = 'form';
