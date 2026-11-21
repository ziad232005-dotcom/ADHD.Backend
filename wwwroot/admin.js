'use strict';

// The password stays in this page's memory only. It is never stored.
let password = '';
let current = 'pending';
let data = { pending: [], approved: [], rejected: [] };

const STATUSES = [
  { key: 'pending', label: 'Waiting for review' },
  { key: 'approved', label: 'Approved (shown to parents)' },
  { key: 'rejected', label: 'Rejected' },
];

const $ = (id) => document.getElementById(id);

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function say(text) {
  $('message').textContent = text || '';
}

function call(path, options = {}) {
  return fetch(path, {
    ...options,
    headers: { 'X-Admin-Password': password, 'Content-Type': 'application/json' },
  });
}

function statusKey(s) {
  const value = String(s.status).toLowerCase();
  if (value === 'approved' || value === '1') return 'approved';
  if (value === 'rejected' || value === '2') return 'rejected';
  return 'pending';
}

function formatDate(value) {
  const d = new Date(value);
  return isNaN(d) ? '' : d.toLocaleDateString();
}

// Loading -----------------------------------------------------------------

async function fetchStatus(key) {
  const response = await call(`/api/admin/shadows?status=${key}`);
  if (response.status === 401) throw new Error('Wrong password.');
  if (response.status === 429) throw new Error('Too many tries. Wait a minute.');
  if (!response.ok) throw new Error('The server could not answer. Check that the admin password is set.');
  return (await response.json()).shadows || [];
}

async function loadAll() {
  const [pending, approved, rejected] = await Promise.all(STATUSES.map((s) => fetchStatus(s.key)));
  data = { pending, approved, rejected };
}

async function open() {
  try {
    await loadAll();
  } catch (error) {
    if (!$('board').hidden) say(error.message);
    else $('loginError').textContent = error.message;
    return false;
  }
  $('login').hidden = true;
  $('board').hidden = false;
  $('logout').hidden = false;
  say('');
  showList();
  return true;
}

// List --------------------------------------------------------------------

function renderStats() {
  const box = $('stats');
  box.replaceChildren();
  for (const s of STATUSES) {
    const tile = el('button', 'stat' + (current === s.key ? ' on' : ''));
    tile.append(el('b', '', String(data[s.key].length)), el('span', '', s.label));
    tile.addEventListener('click', () => {
      current = s.key;
      showList();
    });
    box.append(tile);
  }
}

function renderList() {
  const list = $('list');
  list.replaceChildren();

  const term = $('search').value.trim().toLowerCase();
  const rows = data[current].filter((s) =>
    !term || [s.name, s.governorate, s.qualification, s.university, s.mobile]
      .some((v) => (v || '').toLowerCase().includes(term)));

  if (rows.length === 0) {
    list.append(el('div', 'empty', term ? 'No match.' : 'Nothing here.'));
    return;
  }

  for (const s of rows) {
    const item = el('button', 'item');
    const who = el('div', 'who');
    who.append(
      el('b', '', s.name),
      el('span', '', [s.governorate, s.qualification, s.yearsExperience ? `${s.yearsExperience} years` : '']
        .filter(Boolean).join(' | ')));
    const key = statusKey(s);
    item.append(who, el('span', 'when', formatDate(s.createdAt)), el('span', `badge ${key}`, key));
    item.addEventListener('click', () => showDetail(s));
    list.append(item);
  }
}

function showList() {
  $('detailView').hidden = true;
  $('listView').hidden = false;
  renderStats();
  renderList();
}

// Detail ------------------------------------------------------------------

function group(title, pairs) {
  const rows = pairs.filter(([, value]) => value);
  if (rows.length === 0) return null;
  const box = el('div', 'group');
  box.append(el('h3', '', title));
  const dl = el('dl');
  for (const [label, value] of rows) dl.append(el('dt', '', label), el('dd', '', String(value)));
  box.append(dl);
  return box;
}

async function showFile(path, name, holder) {
  const response = await call(path);
  if (!response.ok) {
    say('The file was not found.');
    return;
  }
  const blob = await response.blob();
  const url = URL.createObjectURL(blob);

  if (blob.type.startsWith('image/')) {
    const image = el('img', 'cert');
    image.alt = name;
    image.src = url;
    holder.replaceChildren(image);
    return;
  }
  const link = document.createElement('a');
  link.href = url;
  link.download = name || 'document.pdf';
  link.click();
}

async function decide(id, action, body) {
  const response = await call(`/api/admin/shadows/${id}/${action}`, {
    method: 'POST',
    body: JSON.stringify(body || {}),
  });
  if (!response.ok) {
    say('Could not save. Try again.');
    return;
  }
  await open();
}

function showDetail(s) {
  say('');
  $('listView').hidden = true;
  const view = $('detailView');
  view.hidden = false;
  view.replaceChildren();

  const key = statusKey(s);
  const card = el('div', 'detail');

  const top = el('div', 'detail-top');
  const heading = el('div');
  heading.append(
    el('h2', '', s.name),
    el('div', 'muted', `Reference ${s.id} | sent ${new Date(s.createdAt).toLocaleString()}`));
  const back = el('button', 'ghost', 'Back to list');
  back.addEventListener('click', showList);
  top.append(heading, el('span', `badge ${key}`, key), back);
  card.append(top);

  const parts = [
    group('Contact (shown to parents)', [['Mobile', s.mobile], ['Email', s.email]]),
    group('Personal', [['Date of birth', formatDate(s.birthDate)], ['Governorate', s.governorate]]),
    group('Education', [
      ['Qualification', s.qualification], ['University', s.university], ['Faculty', s.faculty],
      ['Department', s.department], ['Graduated', s.graduationYear]]),
    group('Experience', [
      ['Years', s.yearsExperience], ['Current workplace', s.currentWorkplace],
      ['Previous workplaces', s.previousWorkplaces], ['With children', s.childExperience],
      ['With ADHD', s.adhdExperience], ['Skills', (s.skills || []).join(', ')]]),
    s.rejectReason ? group('Rejection', [['Reason', s.rejectReason]]) : null,
  ];
  for (const part of parts) if (part) card.append(part);

  // Photo and certificates
  const files = el('div', 'group');
  files.append(el('h3', '', 'Photo and certificates'));
  const buttons = el('div', 'files');
  const holder = el('div');

  if (s.photoFile) {
    const b = el('button', 'ghost', 'Show photo');
    b.addEventListener('click', () => showFile(`/api/admin/shadows/${s.id}/photo`, 'photo', holder));
    buttons.append(b);
  }
  (s.documents || []).forEach((doc, index) => {
    const name = doc.certificateName || doc.kind;
    const details = [doc.issuer, doc.number].filter(Boolean).join(' | ');
    const b = el('button', 'ghost', details ? `${name} (${details})` : name);
    b.addEventListener('click', () =>
      showFile(`/api/admin/shadows/${s.id}/documents/${index}`, doc.fileName, holder));
    buttons.append(b);
  });
  if (!buttons.children.length) buttons.append(el('span', 'muted', 'No files were sent.'));
  files.append(buttons, holder);
  card.append(files);

  // Decision
  const decision = el('div', 'decision');
  const label = el('label', '', 'Note for the record (used as the reason if you reject)');
  const reason = el('textarea');
  reason.rows = 2;
  reason.maxLength = 300;
  const row = el('div', 'buttons');

  if (key !== 'approved') {
    const approve = el('button', 'green', 'Approve and show to parents');
    approve.addEventListener('click', () => decide(s.id, 'approve'));
    row.append(approve);
  }
  if (key !== 'rejected') {
    const reject = el('button', 'red', 'Reject');
    reject.addEventListener('click', () => decide(s.id, 'reject', { reason: reason.value.trim() }));
    row.append(reject);
  }
  decision.append(label, reason, row);
  card.append(decision);

  view.append(card);
  window.scrollTo(0, 0);
}

// Wiring ------------------------------------------------------------------

$('load').addEventListener('click', () => {
  password = $('password').value;
  $('loginError').textContent = '';
  open();
});
$('password').addEventListener('keydown', (e) => { if (e.key === 'Enter') $('load').click(); });
$('search').addEventListener('input', renderList);
$('refresh').addEventListener('click', open);
$('logout').addEventListener('click', () => {
  password = '';
  data = { pending: [], approved: [], rejected: [] };
  $('password').value = '';
  $('board').hidden = true;
  $('logout').hidden = true;
  $('login').hidden = false;
});
