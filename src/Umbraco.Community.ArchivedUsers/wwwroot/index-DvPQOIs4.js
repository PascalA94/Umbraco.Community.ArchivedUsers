import { UmbModalToken as A, UMB_IN_MODAL_CONDITION_ALIAS as d } from "@umbraco-cms/backoffice/modal";
import { UMB_COLLECTION_ALIAS_CONDITION as i } from "@umbraco-cms/backoffice/collection";
import { UMB_WORKSPACE_CONDITION_ALIAS as h } from "@umbraco-cms/backoffice/workspace";
import { UMB_USER_ENTITY_TYPE as e, UMB_USER_COLLECTION_ALIAS as t } from "@umbraco-cms/backoffice/user";
const l = "ArchivedUsers.Modal.ArchiveConfirm", C = new A(
  l,
  { modal: { type: "dialog", size: "small" } }
), a = "archived-users-root", o = "ArchivedUsers.Workspace.Root", s = "ArchivedUsers.Collection.ArchivedUser", r = "ArchivedUsers.Repository.ArchivedUserCollection", U = [
  {
    type: "menuItem",
    alias: "ArchivedUsers.MenuItem.ArchivedUsers",
    name: "Archived Users Menu Item",
    // Just below core's Users (200), so it sits under Users. The menu constant is not publicly exported (S6).
    weight: 190,
    meta: { label: "#archivedUsers_archivedUsers", icon: "icon-box", entityType: a, menus: ["Umb.Menu.UserManagement"] }
  },
  {
    type: "workspace",
    kind: "default",
    alias: o,
    name: "Archived Users Root Workspace",
    meta: { entityType: a, headline: "#archivedUsers_archivedUsers" }
  },
  {
    type: "workspaceView",
    kind: "collection",
    alias: "ArchivedUsers.WorkspaceView.Root.Collection",
    name: "Archived Users Root Collection Workspace View",
    meta: { label: "#archivedUsers_archivedUsers", pathname: "collection", icon: "icon-box", collectionAlias: s },
    conditions: [{ alias: h, match: o }]
  },
  {
    type: "collection",
    kind: "default",
    alias: s,
    name: "Archived Users Collection",
    meta: { repositoryAlias: r }
  },
  {
    type: "repository",
    alias: r,
    name: "Archived Users Collection Repository",
    api: () => import("./archived-user-collection.repository-DLpOCeyE.js")
  },
  {
    type: "collectionView",
    alias: "ArchivedUsers.CollectionView.ArchivedUser.Table",
    name: "Archived Users Table Collection View",
    element: () => import("./archived-user-table.element-DSqcAqR3.js"),
    meta: { label: "Table", icon: "icon-table", pathName: "table" },
    conditions: [{ alias: i, match: s }]
  }
], n = "ArchivedUsers.Condition.User.CanArchive", c = "ArchivedUsers.Condition.User.IsArchived", I = [
  ...U,
  {
    type: "globalContext",
    alias: "ArchivedUsers.GlobalContext.ArchiveStatus",
    name: "Archived Users Archive Status Context",
    api: () => import("./archive-status.context-Bxt3Echy.js")
  },
  {
    type: "condition",
    alias: n,
    name: "Archived Users Can Archive Condition",
    api: () => import("./can-archive.condition-B1biIV4V.js")
  },
  {
    type: "condition",
    alias: c,
    name: "Archived Users Is Archived Condition",
    api: () => import("./is-archived.condition-iN_Wmzbr.js")
  },
  {
    type: "modal",
    alias: l,
    name: "Archived Users Archive Confirm Modal",
    element: () => import("./archive-confirm.element-hZvinb3s.js")
  },
  {
    type: "entityAction",
    kind: "default",
    alias: "ArchivedUsers.EntityAction.User.Archive",
    name: "Archive User",
    weight: 450,
    forEntityTypes: [e],
    api: () => import("./archive.action-BxpZeqe1.js"),
    meta: { icon: "icon-box", label: "#archivedUsers_archive" },
    conditions: [{ alias: n }]
  },
  {
    type: "entityAction",
    kind: "default",
    alias: "ArchivedUsers.EntityAction.User.Unarchive",
    name: "Unarchive User",
    weight: 449,
    forEntityTypes: [e],
    api: () => import("./unarchive.action-CUE3wehP.js"),
    meta: { icon: "icon-undo", label: "#archivedUsers_unarchive" },
    conditions: [{ alias: c }]
  },
  {
    type: "entityBulkAction",
    kind: "default",
    alias: "ArchivedUsers.EntityBulkAction.User.Archive",
    name: "Archive Users",
    weight: 50,
    forEntityTypes: [e],
    api: () => import("./archive.bulk-action-Di765B7T.js"),
    meta: { icon: "icon-box", label: "#archivedUsers_archive" },
    conditions: [{ alias: i, match: t }]
  },
  {
    type: "entityBulkAction",
    kind: "default",
    alias: "ArchivedUsers.EntityBulkAction.User.Unarchive",
    name: "Unarchive Users",
    weight: 50,
    forEntityTypes: [e],
    api: () => import("./unarchive.bulk-action-39F61mYC.js"),
    meta: { icon: "icon-undo", label: "#archivedUsers_unarchive" },
    conditions: [{ alias: i, match: s }]
  },
  {
    type: "collectionAction",
    kind: "button",
    alias: "ArchivedUsers.CollectionAction.User.ArchiveDisabled",
    name: "Archive Disabled Users",
    weight: 50,
    api: () => import("./archive-disabled.action-CfQNfamc.js"),
    meta: { label: "#archivedUsers_archiveDisabled" },
    // Same conditions as core's Create action: the Users collection, never inside a picker modal (F3).
    conditions: [
      { alias: i, match: t },
      { alias: d, match: !1 }
    ]
  },
  {
    type: "localization",
    alias: "ArchivedUsers.Localization.En",
    name: "Archived Users English",
    meta: { culture: "en" },
    js: () => import("./en-CZU-XPv2.js")
  },
  {
    type: "localization",
    alias: "ArchivedUsers.Localization.EnGb",
    name: "Archived Users English (UK)",
    meta: { culture: "en-gb" },
    js: () => import("./en-gb-CZU-XPv2.js")
  }
];
export {
  C as A,
  I as m
};
