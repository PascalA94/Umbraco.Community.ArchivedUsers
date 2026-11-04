function r(e) {
  return !(e.isSelf || e.state === "Invited" || e.targetIsSuperUser || e.targetIsAdmin && !e.performerIsAdmin || e.isArchived && !e.isStale);
}
function t(e) {
  return e.state === "Disabled" && e.lastLoginDate === null && !0;
}
export {
  r as c,
  t as s
};
