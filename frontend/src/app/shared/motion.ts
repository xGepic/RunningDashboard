/** Whether the system's "reduce motion" setting is on; never where the browser can't tell, e.g. in tests. */
export function prefersReducedMotion(): boolean {
  return typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
}
