// src/conditions/predicates.ts
function e(e) {
	return !(e.isSelf || e.state === "Invited" || e.targetIsSuperUser || e.targetIsAdmin && !e.performerIsAdmin || e.isArchived && !e.isStale);
}
function t(e) {
	return e.state === "Disabled" && e.lastLoginDate === null && !e.isArchived;
}
export { t as n, e as t };
