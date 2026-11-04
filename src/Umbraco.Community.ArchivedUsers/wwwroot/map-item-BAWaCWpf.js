// src/archived-view/map-item.ts
function e(e) {
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
function t(e, t) {
	return e.archivedBySystem ? t.system : e.archivedByName || t.removedUser;
}
export { e as n, t };
