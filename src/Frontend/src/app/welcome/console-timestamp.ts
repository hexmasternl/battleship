// Pinned to en-US with a 12-hour clock regardless of the browser locale, so the line format from
// the issue (`[ 10:18:37 AM ] - `) is stable everywhere.
const formatter = new Intl.DateTimeFormat('en-US', {
  hour12: true,
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
});

/** Formats a console line prefix as `[ hh:mm:ss AM ] - ` in the visitor's local time. */
export function formatConsoleTimestamp(date: Date): string {
  // Some engines separate the time and the day period with a narrow no-break space (U+202F).
  const time = formatter.format(date).replace(/ /g, ' ');
  return `[ ${time} ] - `;
}
