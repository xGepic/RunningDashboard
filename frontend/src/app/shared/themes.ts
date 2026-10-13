/** The dashboard's colours, picked in Settings; styles.scss has each Theme's colours. */
export type Theme = 'blue' | 'teal' | 'green' | 'orange' | 'red' | 'pink' | 'purple';

/** In the order Settings offers them: around the colour wheel, from Blue. */
export const themes: { theme: Theme; name: string }[] = [
  { theme: 'blue', name: 'Blue' },
  { theme: 'teal', name: 'Teal' },
  { theme: 'green', name: 'Green' },
  { theme: 'orange', name: 'Orange' },
  { theme: 'red', name: 'Red' },
  { theme: 'pink', name: 'Pink' },
  { theme: 'purple', name: 'Purple' },
];
