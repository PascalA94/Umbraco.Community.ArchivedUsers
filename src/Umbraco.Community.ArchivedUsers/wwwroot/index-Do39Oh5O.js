import { UmbModalToken as n, UMB_IN_MODAL_CONDITION_ALIAS as o } from "@umbraco-cms/backoffice/modal";
import { UMB_USER_ENTITY_TYPE as i, UMB_USER_COLLECTION_ALIAS as t } from "@umbraco-cms/backoffice/user";
import { UMB_COLLECTION_ALIAS_CONDITION as e } from "@umbraco-cms/backoffice/collection";
const r = "ArchivedUsers.Modal.ArchiveConfirm", d = new n(
  r,
  { modal: { type: "dialog", size: "small" } }
), a = "ArchivedUsers.Condition.User.CanArchive", s = "ArchivedUsers.Condition.User.IsArchived", c = "ArchivedUsers.Collection.ArchivedUser", U = [
  {
    type: "globalContext",
    alias: "ArchivedUsers.GlobalContext.ArchiveStatus",
    name: "Archived Users Archive Status Context",
    api: () => import("./archive-status.context-zyd-Xpr5.js")
  },
  {
    type: "condition",
    alias: a,
    name: "Archived Users Can Archive Condition",
    api: () => import("./can-archive.condition-DU_Kt3x8.js")
  },
  {
    type: "condition",
    alias: s,
    name: "Archived Users Is Archived Condition",
    api: () => import("./is-archived.condition-iN_Wmzbr.js")
  },
  {
    type: "modal",
    alias: r,
    name: "Archived Users Archive Confirm Modal",
    element: () => import("./archive-confirm.element-BpmWGceP.js")
  },
  {
    type: "entityAction",
    kind: "default",
    alias: "ArchivedUsers.EntityAction.User.Archive",
    name: "Archive User",
    weight: 450,
    forEntityTypes: [i],
    api: () => import("./archive.action-BiqI6vcQ.js"),
    meta: { icon: "icon-box", label: "#archivedUsers_archive" },
    conditions: [{ alias: a }]
  },
  {
    type: "entityAction",
    kind: "default",
    alias: "ArchivedUsers.EntityAction.User.Unarchive",
    name: "Unarchive User",
    weight: 449,
    forEntityTypes: [i],
    api: () => import("./unarchive.action-Dhwva5HL.js"),
    meta: { icon: "icon-undo", label: "#archivedUsers_unarchive" },
    conditions: [{ alias: s }]
  },
  {
    type: "entityBulkAction",
    kind: "default",
    alias: "ArchivedUsers.EntityBulkAction.User.Archive",
    name: "Archive Users",
    weight: 50,
    forEntityTypes: [i],
    api: () => import("./archive.bulk-action-CrJsDIHa.js"),
    meta: { icon: "icon-box", label: "#archivedUsers_archive" },
    conditions: [{ alias: e, match: t }]
  },
  {
    type: "entityBulkAction",
    kind: "default",
    alias: "ArchivedUsers.EntityBulkAction.User.Unarchive",
    name: "Unarchive Users",
    weight: 50,
    forEntityTypes: [i],
    api: () => import("./unarchive.bulk-action-BpVPkaTr.js"),
    meta: { icon: "icon-undo", label: "#archivedUsers_unarchive" },
    conditions: [{ alias: e, match: c }]
  },
  {
    type: "collectionAction",
    kind: "button",
    alias: "ArchivedUsers.CollectionAction.User.ArchiveDisabled",
    name: "Archive Disabled Users",
    weight: 50,
    api: () => import("./archive-disabled.action-D8HExw_1.js"),
    meta: { label: "#archivedUsers_archiveDisabled" },
    // Same conditions as core's Create action: the Users collection, never inside a picker modal (F3).
    conditions: [
      { alias: e, match: t },
      { alias: o, match: !1 }
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
  d as A,
  U as m
};
