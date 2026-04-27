/**
 * Public /tables JSON may expose assigned server as camelCase or PascalCase.
 * @param {Record<string, unknown> | null | undefined} row
 * @returns {string}
 */
export function tableServerName(row) {
  if (!row) return ''
  const v = row.assignedServerName ?? row.AssignedServerName
  if (v == null || v === '') return ''
  return String(v).trim()
}
