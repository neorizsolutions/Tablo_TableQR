/** An authorization failure cannot prove whether an earlier attempt committed. */
export function clearDefinitivelyFailedOperation(
  storage: Pick<Storage, "removeItem">,
  key: string,
  status: number | undefined,
): boolean {
  if (status !== undefined && [400, 404, 409].includes(status)) {
    storage.removeItem(key);
    return true;
  }
  return false;
}
