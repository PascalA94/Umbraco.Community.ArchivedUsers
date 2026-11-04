import { umbHttpClient as X } from "@umbraco-cms/backoffice/http-client";
import { tryExecute as E } from "@umbraco-cms/backoffice/resources";
const Y = {
  bodySerializer: (e) => JSON.stringify(e, (t, r) => typeof r == "bigint" ? r.toString() : r)
};
function Z({
  onRequest: e,
  onSseError: t,
  onSseEvent: r,
  responseTransformer: a,
  responseValidator: o,
  sseDefaultRetryDelay: c,
  sseMaxRetryAttempts: n,
  sseMaxRetryDelay: s,
  sseSleepFn: i,
  url: m,
  ...u
}) {
  let l;
  const w = i ?? ((d) => new Promise((h) => setTimeout(h, d)));
  return { stream: async function* () {
    let d = c ?? 3e3, h = 0;
    const g = u.signal ?? new AbortController().signal;
    for (; !g.aborted; ) {
      h++;
      const U = u.headers instanceof Headers ? u.headers : new Headers(u.headers);
      l !== void 0 && U.set("Last-Event-ID", l);
      try {
        const j = {
          redirect: "follow",
          ...u,
          body: u.serializedBody,
          headers: U,
          signal: g
        };
        let z = new Request(m, j);
        e && (z = await e(m, j));
        const p = await (u.fetch ?? globalThis.fetch)(z);
        if (!p.ok) throw new Error(`SSE failed: ${p.status} ${p.statusText}`);
        if (!p.body) throw new Error("No body in SSE response");
        const v = p.body.pipeThrough(new TextDecoderStream()).getReader();
        let y = "";
        const k = () => {
          try {
            v.cancel();
          } catch {
          }
        };
        g.addEventListener("abort", k);
        try {
          for (; ; ) {
            const { done: Q, value: G } = await v.read();
            if (Q) break;
            y += G, y = y.replace(/\r\n?/g, `
`);
            const I = y.split(`

`);
            y = I.pop() ?? "";
            for (const M of I) {
              const K = M.split(`
`), O = [];
              let B;
              for (const S of K)
                if (S.startsWith("data:"))
                  O.push(S.replace(/^data:\s*/, ""));
                else if (S.startsWith("event:"))
                  B = S.replace(/^event:\s*/, "");
                else if (S.startsWith("id:"))
                  l = S.replace(/^id:\s*/, "");
                else if (S.startsWith("retry:")) {
                  const P = Number.parseInt(S.replace(/^retry:\s*/, ""), 10);
                  Number.isNaN(P) || (d = P);
                }
              let A, N = !1;
              if (O.length) {
                const S = O.join(`
`);
                try {
                  A = JSON.parse(S), N = !0;
                } catch {
                  A = S;
                }
              }
              N && (o && await o(A), a && (A = await a(A))), r?.({
                data: A,
                event: B,
                id: l,
                retry: d
              }), O.length && (yield A);
            }
          }
        } finally {
          g.removeEventListener("abort", k), v.releaseLock();
        }
        break;
      } catch (j) {
        if (t?.(j), n !== void 0 && h >= n)
          break;
        const z = Math.min(d * 2 ** (h - 1), s ?? 3e4);
        await w(z);
      }
    }
  }() };
}
const ee = (e) => {
  switch (e) {
    case "label":
      return ".";
    case "matrix":
      return ";";
    case "simple":
      return ",";
    default:
      return "&";
  }
}, te = (e) => {
  switch (e) {
    case "form":
      return ",";
    case "pipeDelimited":
      return "|";
    case "spaceDelimited":
      return "%20";
    default:
      return ",";
  }
}, re = (e) => {
  switch (e) {
    case "label":
      return ".";
    case "matrix":
      return ";";
    case "simple":
      return ",";
    default:
      return "&";
  }
}, V = ({
  allowReserved: e,
  explode: t,
  name: r,
  style: a,
  value: o
}) => {
  if (!t) {
    const s = (e ? o : o.map((i) => encodeURIComponent(i))).join(te(a));
    switch (a) {
      case "label":
        return `.${s}`;
      case "matrix":
        return `;${r}=${s}`;
      case "simple":
        return s;
      default:
        return `${r}=${s}`;
    }
  }
  const c = ee(a), n = o.map((s) => a === "label" || a === "simple" ? e ? s : encodeURIComponent(s) : T({
    allowReserved: e,
    name: r,
    value: s
  })).join(c);
  return a === "label" || a === "matrix" ? c + n : n;
}, T = ({
  allowReserved: e,
  name: t,
  value: r
}) => {
  if (r == null)
    return "";
  if (typeof r == "object")
    throw new Error(
      "Deeply-nested arrays/objects aren’t supported. Provide your own `querySerializer()` to handle these."
    );
  return `${t}=${e ? r : encodeURIComponent(r)}`;
}, L = ({
  allowReserved: e,
  explode: t,
  name: r,
  style: a,
  value: o,
  valueOnly: c
}) => {
  if (o instanceof Date)
    return c ? o.toISOString() : `${r}=${o.toISOString()}`;
  if (a !== "deepObject" && !t) {
    let i = [];
    Object.entries(o).forEach(([u, l]) => {
      i = [...i, u, e ? l : encodeURIComponent(l)];
    });
    const m = i.join(",");
    switch (a) {
      case "form":
        return `${r}=${m}`;
      case "label":
        return `.${m}`;
      case "matrix":
        return `;${r}=${m}`;
      default:
        return m;
    }
  }
  const n = re(a), s = Object.entries(o).map(
    ([i, m]) => T({
      allowReserved: e,
      name: a === "deepObject" ? `${r}[${i}]` : i,
      value: m
    })
  ).join(n);
  return a === "label" || a === "matrix" ? n + s : s;
}, se = /\{[^{}]+\}/g, ae = ({ path: e, url: t }) => {
  let r = t;
  const a = t.match(se);
  if (a)
    for (const o of a) {
      let c = !1, n = o.substring(1, o.length - 1), s = "simple";
      n.endsWith("*") && (c = !0, n = n.substring(0, n.length - 1)), n.startsWith(".") ? (n = n.substring(1), s = "label") : n.startsWith(";") && (n = n.substring(1), s = "matrix");
      const i = e[n];
      if (i == null)
        continue;
      if (Array.isArray(i)) {
        r = r.replace(o, V({ explode: c, name: n, style: s, value: i }));
        continue;
      }
      if (typeof i == "object") {
        r = r.replace(
          o,
          L({
            explode: c,
            name: n,
            style: s,
            value: i,
            valueOnly: !0
          })
        );
        continue;
      }
      if (s === "matrix") {
        r = r.replace(
          o,
          `;${T({
            name: n,
            value: i
          })}`
        );
        continue;
      }
      const m = encodeURIComponent(
        s === "label" ? `.${i}` : i
      );
      r = r.replace(o, m);
    }
  return r;
}, ne = ({
  baseUrl: e,
  path: t,
  query: r,
  querySerializer: a,
  url: o
}) => {
  const c = o.startsWith("/") ? o : `/${o}`;
  let n = (e ?? "") + c;
  t && (n = ae({ path: t, url: n }));
  let s = r ? a(r) : "";
  return s.startsWith("?") && (s = s.substring(1)), s && (n += `?${s}`), n;
};
function R(e) {
  const t = e.body !== void 0;
  if (t && e.bodySerializer)
    return "serializedBody" in e ? e.serializedBody !== void 0 && e.serializedBody !== "" ? e.serializedBody : null : e.body !== "" ? e.body : null;
  if (t)
    return e.body;
}
const ie = async (e, t) => {
  const r = typeof t == "function" ? await t(e) : t;
  if (r)
    return e.scheme === "bearer" ? `Bearer ${r}` : e.scheme === "basic" ? `Basic ${btoa(r)}` : r;
}, J = ({
  parameters: e = {},
  ...t
} = {}) => (a) => {
  const o = [];
  if (a && typeof a == "object")
    for (const c in a) {
      const n = a[c];
      if (n == null)
        continue;
      const s = e[c] || t;
      if (Array.isArray(n)) {
        const i = V({
          allowReserved: s.allowReserved,
          explode: !0,
          name: c,
          style: "form",
          value: n,
          ...s.array
        });
        i && o.push(i);
      } else if (typeof n == "object") {
        const i = L({
          allowReserved: s.allowReserved,
          explode: !0,
          name: c,
          style: "deepObject",
          value: n,
          ...s.object
        });
        i && o.push(i);
      } else {
        const i = T({
          allowReserved: s.allowReserved,
          name: c,
          value: n
        });
        i && o.push(i);
      }
    }
  return o.join("&");
}, oe = (e) => {
  if (!e)
    return "stream";
  const t = e.split(";")[0]?.trim();
  if (t) {
    if (t.startsWith("application/json") || t.endsWith("+json"))
      return "json";
    if (t === "multipart/form-data")
      return "formData";
    if (["application/", "audio/", "image/", "video/"].some((r) => t.startsWith(r)))
      return "blob";
    if (t.startsWith("text/"))
      return "text";
  }
}, ce = (e, t) => t ? !!(e.headers.has(t) || e.query?.[t] || e.headers.get("Cookie")?.includes(`${t}=`)) : !1;
async function le(e) {
  for (const t of e.security ?? []) {
    if (ce(e, t.name))
      continue;
    const r = await ie(t, e.auth);
    if (!r)
      continue;
    const a = t.name ?? "Authorization";
    switch (t.in) {
      case "query":
        e.query || (e.query = {}), e.query[a] = r;
        break;
      case "cookie":
        e.headers.append("Cookie", `${a}=${r}`);
        break;
      default:
        e.headers.set(a, r);
        break;
    }
  }
}
const D = (e) => ne({
  baseUrl: e.baseUrl,
  path: e.path,
  query: e.query,
  querySerializer: typeof e.querySerializer == "function" ? e.querySerializer : J(e.querySerializer),
  url: e.url
}), H = (e, t) => {
  const r = { ...e, ...t };
  return r.baseUrl?.endsWith("/") && (r.baseUrl = r.baseUrl.substring(0, r.baseUrl.length - 1)), r.headers = _(e.headers, t.headers), r;
}, ue = (e) => {
  const t = [];
  return e.forEach((r, a) => {
    t.push([a, r]);
  }), t;
}, _ = (...e) => {
  const t = new Headers();
  for (const r of e) {
    if (!r)
      continue;
    const a = r instanceof Headers ? ue(r) : Object.entries(r);
    for (const [o, c] of a)
      if (c === null)
        t.delete(o);
      else if (Array.isArray(c))
        for (const n of c)
          t.append(o, n);
      else c !== void 0 && t.set(
        o,
        typeof c == "object" ? JSON.stringify(c) : c
      );
  }
  return t;
};
class $ {
  constructor() {
    this.fns = [];
  }
  clear() {
    this.fns = [];
  }
  eject(t) {
    const r = this.getInterceptorIndex(t);
    this.fns[r] && (this.fns[r] = null);
  }
  exists(t) {
    const r = this.getInterceptorIndex(t);
    return !!this.fns[r];
  }
  getInterceptorIndex(t) {
    return typeof t == "number" ? this.fns[t] ? t : -1 : this.fns.indexOf(t);
  }
  update(t, r) {
    const a = this.getInterceptorIndex(t);
    return this.fns[a] ? (this.fns[a] = r, t) : !1;
  }
  use(t) {
    return this.fns.push(t), this.fns.length - 1;
  }
}
const de = () => ({
  error: new $(),
  request: new $(),
  response: new $()
}), fe = J({
  allowReserved: !1,
  array: {
    explode: !0,
    style: "form"
  },
  object: {
    explode: !0,
    style: "deepObject"
  }
}), he = {
  "Content-Type": "application/json"
}, F = (e = {}) => ({
  ...Y,
  headers: he,
  parseAs: "auto",
  querySerializer: fe,
  ...e
}), pe = (e = {}) => {
  let t = H(F(), e);
  const r = () => ({ ...t }), a = (u) => (t = H(t, u), r()), o = de(), c = async (u) => {
    const l = {
      ...t,
      ...u,
      fetch: u.fetch ?? t.fetch ?? globalThis.fetch,
      headers: _(t.headers, u.headers),
      serializedBody: void 0
    };
    l.security && await le(l), l.requestValidator && await l.requestValidator(l), l.body !== void 0 && l.bodySerializer && (l.serializedBody = l.bodySerializer(l.body)), (l.body === void 0 || l.serializedBody === "") && l.headers.delete("Content-Type");
    const w = l, b = D(w);
    return { opts: w, url: b };
  }, n = async (u) => {
    const l = u.throwOnError ?? t.throwOnError, w = u.responseStyle ?? t.responseStyle;
    let b, f;
    try {
      const { opts: d, url: h } = await c(u), g = {
        redirect: "follow",
        ...d,
        body: R(d)
      };
      b = new Request(h, g);
      for (const p of o.request.fns)
        p && (b = await p(b, d));
      const U = d.fetch;
      f = await U(b);
      for (const p of o.response.fns)
        p && (f = await p(f, b, d));
      const j = {
        request: b,
        response: f
      };
      if (f.ok) {
        const p = (d.parseAs === "auto" ? oe(f.headers.get("Content-Type")) : d.parseAs) ?? "json";
        if (f.status === 204 || f.headers.get("Content-Length") === "0") {
          let y;
          switch (p) {
            case "arrayBuffer":
            case "blob":
            case "text":
              y = await f[p]();
              break;
            case "formData":
              y = new FormData();
              break;
            case "stream":
              y = f.body;
              break;
            default:
              y = {};
              break;
          }
          return d.responseStyle === "data" ? y : {
            data: y,
            ...j
          };
        }
        let v;
        switch (p) {
          case "arrayBuffer":
          case "blob":
          case "formData":
          case "text":
            v = await f[p]();
            break;
          case "json": {
            const y = await f.text();
            v = y ? JSON.parse(y) : {};
            break;
          }
          case "stream":
            return d.responseStyle === "data" ? f.body : {
              data: f.body,
              ...j
            };
        }
        return p === "json" && (d.responseValidator && await d.responseValidator(v), d.responseTransformer && (v = await d.responseTransformer(v))), d.responseStyle === "data" ? v : {
          data: v,
          ...j
        };
      }
      const z = await f.text();
      let q;
      try {
        q = JSON.parse(z);
      } catch {
      }
      throw q ?? z;
    } catch (d) {
      let h = d;
      for (const g of o.error.fns)
        g && (h = await g(h, f, b, u));
      if (h = h || {}, l)
        throw h;
      return w === "data" ? void 0 : {
        error: h,
        request: b,
        response: f
      };
    }
  }, s = (u) => (l) => n({ ...l, method: u }), i = (u) => async (l) => {
    const { opts: w, url: b } = await c(l);
    return Z({
      ...w,
      body: w.body,
      method: u,
      onRequest: async (f, d) => {
        let h = new Request(f, d);
        for (const g of o.request.fns)
          g && (h = await g(h, w));
        return h;
      },
      serializedBody: R(w),
      url: b
    });
  };
  return {
    buildUrl: (u) => D({ ...t, ...u }),
    connect: s("CONNECT"),
    delete: s("DELETE"),
    get: s("GET"),
    getConfig: r,
    head: s("HEAD"),
    interceptors: o,
    options: s("OPTIONS"),
    patch: s("PATCH"),
    post: s("POST"),
    put: s("PUT"),
    request: n,
    setConfig: a,
    sse: {
      connect: i("CONNECT"),
      delete: i("DELETE"),
      get: i("GET"),
      head: i("HEAD"),
      options: i("OPTIONS"),
      patch: i("PATCH"),
      post: i("POST"),
      put: i("PUT"),
      trace: i("TRACE")
    },
    trace: s("TRACE")
  };
}, C = pe(F({ baseUrl: "http://localhost:28331" })), W = (e) => (e?.client ?? C).get({
  security: [{ scheme: "bearer", type: "http" }],
  url: "/umbraco/management/api/v1/archived-users",
  ...e
}), ye = (e) => (e.client ?? C).get({
  security: [{ scheme: "bearer", type: "http" }],
  url: "/umbraco/management/api/v1/archived-users/{id}",
  ...e
}), be = (e) => (e?.client ?? C).post({
  security: [{ scheme: "bearer", type: "http" }],
  url: "/umbraco/management/api/v1/archived-users/archive",
  ...e,
  headers: {
    "Content-Type": "application/json",
    ...e?.headers
  }
}), me = (e) => (e?.client ?? C).post({
  security: [{ scheme: "bearer", type: "http" }],
  url: "/umbraco/management/api/v1/archived-users/clean-up",
  ...e,
  headers: {
    "Content-Type": "application/json",
    ...e?.headers
  }
}), ge = (e) => (e?.client ?? C).get({
  security: [{ scheme: "bearer", type: "http" }],
  url: "/umbraco/management/api/v1/archived-users/clean-up/preview",
  ...e
}), we = (e) => (e?.client ?? C).get({
  security: [{ scheme: "bearer", type: "http" }],
  url: "/umbraco/management/api/v1/archived-users/config",
  ...e
}), ve = (e) => (e?.client ?? C).post({
  security: [{ scheme: "bearer", type: "http" }],
  url: "/umbraco/management/api/v1/archived-users/unarchive",
  ...e,
  headers: {
    "Content-Type": "application/json",
    ...e?.headers
  }
}), x = X, Ee = {
  config: (e) => E(e, we({ client: x })),
  getOne: (e, t) => E(e, ye({ client: x, path: { id: t } }), { disableNotifications: !0 }),
  list: (e, t) => E(e, W({ client: x, query: t })),
  /** The archive-status background reload: errors stay quiet, so a failing server can't stack toasts (review A2-4). */
  listQuietly: (e, t) => E(e, W({ client: x, query: t }), { disableNotifications: !0 }),
  archive: (e, t, r) => E(e, be({ client: x, body: { userIds: t, reason: r ?? null } })),
  unarchive: (e, t) => E(e, ve({ client: x, body: { userIds: t, enable: null } })),
  previewCleanUp: (e) => E(e, ge({ client: x })),
  cleanUp: (e, t) => E(e, me({ client: x, body: { reason: t ?? null } }))
};
export {
  Ee as a
};
