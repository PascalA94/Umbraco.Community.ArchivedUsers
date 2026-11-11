import { n as e } from "./archive-confirm.modal-token-vGuUZPCT.js";
import { UMB_IN_MODAL_CONDITION_ALIAS as t } from "@umbraco-cms/backoffice/modal";
import { UMB_COLLECTION_ALIAS_CONDITION as n } from "@umbraco-cms/backoffice/collection";
import { UMB_WORKSPACE_CONDITION_ALIAS as r } from "@umbraco-cms/backoffice/workspace";
import { UMB_USER_COLLECTION_ALIAS as i, UMB_USER_ENTITY_TYPE as a } from "@umbraco-cms/backoffice/user";
// src/archived-view/manifests.ts
var o = "archived-users-root", s = "ArchivedUsers.Workspace.Root", c = "ArchivedUsers.Collection.ArchivedUser", l = "ArchivedUsers.Repository.ArchivedUserCollection", u = [
	{
		type: "menuItem",
		alias: "ArchivedUsers.MenuItem.ArchivedUsers",
		name: "Archived Users Menu Item",
		weight: 90,
		meta: {
			label: "#archivedUsers_archivedUsers",
			icon: "icon-box",
			entityType: o,
			menus: ["Umb.Menu.UserManagement"]
		}
	},
	{
		type: "workspace",
		kind: "default",
		alias: s,
		name: "Archived Users Root Workspace",
		meta: {
			entityType: o,
			headline: "#archivedUsers_archivedUsers"
		}
	},
	{
		type: "workspaceView",
		kind: "collection",
		alias: "ArchivedUsers.WorkspaceView.Root.Collection",
		name: "Archived Users Root Collection Workspace View",
		meta: {
			label: "#archivedUsers_archivedUsers",
			pathname: "collection",
			icon: "icon-box",
			collectionAlias: c
		},
		conditions: [{
			alias: r,
			match: s
		}]
	},
	{
		type: "collection",
		kind: "default",
		alias: c,
		name: "Archived Users Collection",
		meta: { repositoryAlias: l }
	},
	{
		type: "repository",
		alias: l,
		name: "Archived Users Collection Repository",
		api: () => import("./archived-user-collection.repository-VX3Cjg1y.js")
	},
	{
		type: "collectionView",
		alias: "ArchivedUsers.CollectionView.ArchivedUser.Table",
		name: "Archived Users Table Collection View",
		element: () => import("./archived-user-table.element-D2s2crlX.js"),
		meta: {
			label: "Table",
			icon: "icon-table",
			pathName: "table"
		},
		conditions: [{
			alias: n,
			match: c
		}]
	}
], d = "ArchivedUsers.Condition.User.CanArchive", f = "ArchivedUsers.Condition.User.IsArchived", p = [
	...u,
	{
		type: "globalContext",
		alias: "ArchivedUsers.GlobalContext.ArchiveStatus",
		name: "Archived Users Archive Status Context",
		api: () => import("./archive-status.context-4yoiXzkK.js")
	},
	{
		type: "condition",
		alias: d,
		name: "Archived Users Can Archive Condition",
		api: () => import("./can-archive.condition-vir8WGo8.js")
	},
	{
		type: "condition",
		alias: f,
		name: "Archived Users Is Archived Condition",
		api: () => import("./is-archived.condition-BXaUUpaX.js")
	},
	{
		type: "modal",
		alias: e,
		name: "Archived Users Archive Confirm Modal",
		element: () => import("./archive-confirm.element-Cd1904ek.js")
	},
	{
		type: "entityAction",
		kind: "default",
		alias: "ArchivedUsers.EntityAction.User.Archive",
		name: "Archive User",
		weight: 450,
		forEntityTypes: [a],
		api: () => import("./archive.action-B9OtSkZG.js"),
		meta: {
			icon: "icon-box",
			label: "#archivedUsers_archive"
		},
		conditions: [{ alias: d }]
	},
	{
		type: "entityAction",
		kind: "default",
		alias: "ArchivedUsers.EntityAction.User.Unarchive",
		name: "Unarchive User",
		weight: 449,
		forEntityTypes: [a],
		api: () => import("./unarchive.action-LgxxG1_b.js"),
		meta: {
			icon: "icon-undo",
			label: "#archivedUsers_unarchive"
		},
		conditions: [{ alias: f }]
	},
	{
		type: "entityBulkAction",
		kind: "default",
		alias: "ArchivedUsers.EntityBulkAction.User.Archive",
		name: "Archive Users",
		weight: 50,
		forEntityTypes: [a],
		api: () => import("./archive.bulk-action-CiA_TR2F.js"),
		meta: {
			icon: "icon-box",
			label: "#archivedUsers_archive"
		},
		conditions: [{
			alias: n,
			match: i
		}]
	},
	{
		type: "entityBulkAction",
		kind: "default",
		alias: "ArchivedUsers.EntityBulkAction.User.Unarchive",
		name: "Unarchive Users",
		weight: 50,
		forEntityTypes: [a],
		api: () => import("./unarchive.bulk-action-BaoEnAsT.js"),
		meta: {
			icon: "icon-undo",
			label: "#archivedUsers_unarchive"
		},
		conditions: [{
			alias: n,
			match: c
		}]
	},
	{
		type: "collectionAction",
		kind: "button",
		alias: "ArchivedUsers.CollectionAction.User.ArchiveDisabled",
		name: "Archive Disabled Users",
		weight: 50,
		api: () => import("./archive-disabled.action-CzlJuP4G.js"),
		meta: { label: "#archivedUsers_archiveDisabled" },
		conditions: [{
			alias: n,
			match: i
		}, {
			alias: t,
			match: !1
		}]
	},
	{
		type: "localization",
		alias: "ArchivedUsers.Localization.En",
		name: "Archived Users English",
		meta: { culture: "en" },
		js: () => import("./en-HmyGknU1.js")
	},
	{
		type: "localization",
		alias: "ArchivedUsers.Localization.EnGb",
		name: "Archived Users English (UK)",
		meta: { culture: "en-gb" },
		js: () => import("./en-gb-DBhfwbe1.js")
	}
];
export { p as manifests };
