import { UmbModalToken as r } from "@umbraco-cms/backoffice/modal";
import { UMB_USER_ENTITY_TYPE as i } from "@umbraco-cms/backoffice/user";
const a = "ArchivedUsers.Modal.ArchiveConfirm", o = new r(
  a,
  { modal: { type: "dialog", size: "small" } }
), e = "ArchivedUsers.Condition.User.CanArchive", t = "ArchivedUsers.Condition.User.IsArchived", c = [
  {
    type: "globalContext",
    alias: "ArchivedUsers.GlobalContext.ArchiveStatus",
    name: "Archived Users Archive Status Context",
    api: () => import("./archive-status.context-zyd-Xpr5.js")
  },
  {
    type: "condition",
    alias: e,
    name: "Archived Users Can Archive Condition",
    api: () => import("./can-archive.condition-DU_Kt3x8.js")
  },
  {
    type: "condition",
    alias: t,
    name: "Archived Users Is Archived Condition",
    api: () => import("./is-archived.condition-iN_Wmzbr.js")
  },
  {
    type: "modal",
    alias: a,
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
    api: () => import("./archive.action-CZ0OjYTj.js"),
    meta: { icon: "icon-box", label: "#archivedUsers_archive" },
    conditions: [{ alias: e }]
  },
  {
    type: "entityAction",
    kind: "default",
    alias: "ArchivedUsers.EntityAction.User.Unarchive",
    name: "Unarchive User",
    weight: 449,
    forEntityTypes: [i],
    api: () => import("./unarchive.action-CStWq8eN.js"),
    meta: { icon: "icon-undo", label: "#archivedUsers_unarchive" },
    conditions: [{ alias: t }]
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
  o as A,
  c as m
};
