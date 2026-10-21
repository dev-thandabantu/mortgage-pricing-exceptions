"use strict";
const $ = id => document.getElementById(id);
let users = [], selected = null, createAttempt = null, viewGeneration = 0, saving = false;
const user = () => users.find(value => value.id === $("identity").value);
function notify(text, error = false) {
  $("message").textContent = text;
  $("message").className = error ? "error" : "";
  $("message").hidden = false;
}
async function api(path, options = {}) {
  const response = await fetch(path, { ...options, headers: { "Content-Type": "application/json", "X-User-Id": $("identity").value, ...options.headers } });
  const body = await response.json();
  if (!response.ok) throw new Error(body.detail || body.title || `Request failed (${response.status}).`);
  return body;
}
function text(tag, value, className) {
  const element = document.createElement(tag);
  element.textContent = value;
  if (className) element.className = className;
  return element;
}
const date = value => new Date(value).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
function applyPermissions() {
  const actor = user();
  const canCreate = actor?.roles.includes("RelationshipManager") || false;
  for (const element of $("create-form").elements) element.disabled = saving || !canCreate;
  $("identity").disabled = $("refresh").disabled = saving;
  for (const button of document.querySelectorAll(".request-row")) button.disabled = saving;
  $("create-hint").textContent = canCreate ? "Choose an application and explain the proposed discount." : "Select a relationship manager to submit a request.";
  if (!selected || !actor) return;
  const pending = selected.status === "Pending", own = selected.createdBy === actor.id;
  $("amend-form").hidden = !(pending && own && actor.roles.includes("RelationshipManager"));
  $("decision-form").hidden = !(pending && !own && actor.roles.includes("Reviewer"));
  for (const form of [$("amend-form"), $("decision-form")])
    for (const element of form.elements) element.disabled = saving;
  $("decision-hint").textContent = !pending ? "This decision is final. The reviewed terms are preserved below." : own ? "A different authorised reviewer must decide this request." : actor.roles.includes("Reviewer") ? `Your decision will apply to revision ${selected.revisionNumber}.` : "Select an authorised reviewer to decide this request.";
}
async function refresh(id = selected?.id) {
  const generation = ++viewGeneration;
  const requests = await api("/pricing-exceptions");
  if (generation !== viewGeneration) return;
  $("pending-count").textContent = requests.filter(value => value.status === "Pending").length;
  $("approved-count").textContent = requests.filter(value => value.status === "Approved").length;
  $("declined-count").textContent = requests.filter(value => value.status === "Declined").length;
  $("queue-count").textContent = `${requests.length} shown \u00b7 latest 100`;
  $("request-list").replaceChildren();
  if (!requests.length) $("request-list").append(text("p", "No requests yet. Submit an exception to begin.", "empty"));
  for (const request of requests) {
    const button = text("button", "", "request-row");
    button.type = "button";
    button.dataset.id = request.id;
    button.setAttribute("aria-pressed", String(request.id === id));
    const top = text("span", "", "row-top");
    top.append(text("strong", request.applicationId), text("span", request.status === "Pending" ? "Awaiting review" : request.status, `badge ${request.status}`));
    const bottom = text("span", "", "row-bottom");
    bottom.append(text("span", `${request.discountBps} bps \u00b7 ${request.createdBy}`), text("span", date(request.createdAt)));
    button.append(top, bottom);
    button.addEventListener("click", () => run(() => select(request.id)));
    $("request-list").append(button);
  }
  applyPermissions();
  if (id) await select(id);
}
async function select(id) {
  const generation = ++viewGeneration;
  const request = await api(`/pricing-exceptions/${encodeURIComponent(id)}`);
  if (generation !== viewGeneration) return;
  renderDetail(request);
  for (const button of document.querySelectorAll(".request-row")) button.setAttribute("aria-pressed", String(button.dataset.id === id));
}
function renderDetail(request) {
  selected = request;
  const pending = request.status === "Pending";
  $("empty-detail").hidden = true;
  $("detail").hidden = false;
  $("status").hidden = false;
  $("status").textContent = pending ? "Awaiting review" : request.status;
  $("status").className = `badge ${request.status}`;
  $("detail-discount").textContent = `${request.discountBps} bps`;
  $("detail-revision").textContent = `Revision ${request.revisionNumber} \u00b7 ${(request.discountBps / 100).toFixed(2)} percentage points`;
  $("detail-application").textContent = request.applicationId;
  $("detail-author").textContent = `Submitted by ${request.createdBy}`;
  $("detail-reason").textContent = request.reason;
  $("amend-discount").value = request.discountBps;
  $("amend-reason").value = request.reason;
  $("comment").value = "";
  $("history").replaceChildren();
  for (const revision of request.revisions) {
    const item = document.createElement("li");
    item.append(text("strong", `${revision.number === 1 ? "Submitted" : "Amended"} \u00b7 revision ${revision.number} \u00b7 ${revision.discountBps} bps`), text("p", revision.reason, "note"), text("small", `${revision.author} \u00b7 ${date(revision.createdAt)}`));
    $("history").append(item);
  }
  for (const decision of request.decisions) {
    const item = document.createElement("li");
    item.append(text("strong", `${decision.outcome} \u00b7 revision ${decision.revisionNumber}`));
    if (decision.comment) item.append(text("p", decision.comment, "note"));
    item.append(text("small", `${decision.decidedBy} \u00b7 ${date(decision.decidedAt)}`));
    $("history").append(item);
  }
  $("approved-terms").hidden = request.status !== "Approved";
  if (request.status === "Approved") $("approved-terms").textContent = `Approved discount: ${request.discountBps} bps. Available to the mortgage application process.`;
  applyPermissions();
}
async function run(action) {
  try { await action(); } catch (error) { notify(error.message, true); }
}
async function submit(action) {
  if (saving) return;
  saving = true;
  ++viewGeneration; // A command supersedes any earlier detail/refresh response.
  applyPermissions();
  try { await action(); } catch (error) { notify(error.message, true); }
  finally { saving = false; applyPermissions(); }
}
$("create-form").addEventListener("submit", event => {
  event.preventDefault();
  submit(async () => {
    const body = JSON.stringify({ applicationId: $("application").value, discountBps: Number($("discount").value), reason: $("reason").value });
    if (!createAttempt || createAttempt.body !== body || createAttempt.actor !== user().id) createAttempt = { body, actor: user().id, key: crypto.randomUUID() };
    const request = await api("/pricing-exceptions", { method: "POST", headers: { "Idempotency-Key": createAttempt.key }, body });
    createAttempt = null;
    $("reason").value = "";
    await refresh(request.id);
    notify("Request submitted. A different authorised reviewer can now decide it.");
  });
});
$("amend-form").addEventListener("submit", event => {
  event.preventDefault();
  submit(async () => {
    const request = await api(`/pricing-exceptions/${selected.id}`, { method: "PATCH", body: JSON.stringify({ discountBps: Number($("amend-discount").value), reason: $("amend-reason").value, expectedVersion: selected.version }) });
    await refresh(request.id);
    notify(`Revision ${request.revisionNumber} saved. Earlier terms remain in the history.`);
  });
});
$("decision-form").addEventListener("submit", event => {
  event.preventDefault();
  const outcome = event.submitter.value;
  submit(async () => {
    const request = await api(`/pricing-exceptions/${selected.id}/decisions`, { method: "POST", body: JSON.stringify({ outcome, expectedVersion: selected.version, comment: $("comment").value || null }) });
    await refresh(request.id);
    notify(outcome === "Approved" ? "Discount approved and available to the mortgage process." : "Request declined. The decision has been recorded.");
  });
});
$("refresh").addEventListener("click", () => run(async () => { await refresh(); notify("Requests refreshed."); }));
$("identity").addEventListener("change", () => run(async () => { if (selected) renderDetail(selected); else applyPermissions(); await refresh(); }));
run(async () => {
  const response = await fetch("/demo");
  if (!response.ok) throw new Error("Unable to load the local demo.");
  const demo = await response.json();
  users = demo.users;
  for (const value of users) { const option = text("option", value.name); option.value = value.id; $("identity").append(option); }
  for (const value of demo.applications) { const option = text("option", `${value.id} \u00b7 ${value.description}`); option.value = value.id; $("application").append(option); }
  $("discount").max = $("amend-discount").max = demo.maxDiscountBps;
  applyPermissions();
  await refresh();
});
