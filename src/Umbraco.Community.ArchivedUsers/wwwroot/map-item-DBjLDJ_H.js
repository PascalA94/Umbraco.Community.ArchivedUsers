function r(e) {
  return {
    unique: e.user.id,
    entityType: "user",
    name: e.user.name,
    email: e.user.email,
    archivedUtc: e.archivedUtc,
    archivedByName: e.archivedBy?.name ?? null,
    archivedBySystem: e.archivedBy == null,
    reason: e.reason ?? null,
    lastLoginDate: e.user.lastLoginDate ?? null,
    stale: e.stale
  };
}
function n(e, a) {
  return e.archivedBySystem ? a.system : e.archivedByName || a.removedUser;
}
export {
  n as a,
  r as m
};
