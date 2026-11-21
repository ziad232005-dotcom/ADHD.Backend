'use strict';

// The password stays in this page's memory only. It is never stored.
let password = '';
let status = 'pending';

const $ = (id) => document.getElementById(id);

function say(text) {
  $('message').textContent = text || '';
}

async function call(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: {
      'X-Admin-Password': password,
      'Content-Type': 'application/json',
    },
  });
  return response;
}

function line(parent, label, value) {
  if (!value) return;
  const row = document.createElement('div');
  row.className = 'row';
  const strong = document.createElement('strong');
  strong.textContent = label + ': ';
  row.append(strong, document.createTextNode(value));
  parent.append(row);
}

function button(text, className, onClick) {
  const b = document.createElement('button');
  b.textContent = text;
  if (className) b.className = className;
  b.addEventListener('click', onClick);
  return b;
}

async function review(id, action, body) {
  const response = await call(`/api/admin/shadows/${id}/${action}`, {
    method: 'POST',
    body: JSON.stringify(body || {}),
  });
  if (!response.ok) {
    say('Could not save. Try again.');
    return;
  }
  load();
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
    const image = document.createElement('img');
    image.className = 'cert';
    image.alt = name;
    image.src = url;
    holder.replaceChildren(image);
    return;
  }

  // PDFs are saved, then opened with the device's own PDF viewer.
  const link = document.createElement('a');
  link.href = url;
  link.download = name || 'document.pdf';
  link.click();
}

function render(shadows) {
  const list = $('list');
  list.replaceChildren();

  if (shadows.length === 0) {
    const empty = document.createElement('p');
    empty.className = 'muted';
    empty.textContent = 'Nothing here.';
    list.append(empty);
    return;
  }

  for (const s of shadows) {
    const card = document.createElement('div');
    card.className = 'card';

    const title = document.createElement('h2');
    title.textContent = s.name;
    const meta = document.createElement('div');
    meta.className = 'muted';
    meta.textContent = `Reference ${s.id} | ${new Date(s.createdAt).toLocaleString()}`;
    card.append(title, meta);

    line(card, 'Date of birth', new Date(s.birthDate).toLocaleDateString());
    line(card, 'Mobile', s.mobile);
    line(card, 'Email', s.email);
    line(card, 'Governorate', s.governorate);
    line(card, 'Qualification', s.qualification);
    line(card, 'University', s.university);
    line(card, 'Faculty', s.faculty);
    line(card, 'Department', s.department);
    line(card, 'Graduated', s.graduationYear);
    line(card, 'Experience', s.yearsExperience);
    line(card, 'Current workplace', s.currentWorkplace);
    line(card, 'Previous workplaces', s.previousWorkplaces);
    line(card, 'With children', s.childExperience);
    line(card, 'ADHD', s.adhdExperience);
    line(card, 'Skills', (s.skills || []).join(', '));
    line(card, 'Reason for rejection', s.rejectReason);

    const holder = document.createElement('div');
    const actions = document.createElement('div');
    actions.className = 'actions';

    if (s.photoFile) {
      actions.append(button('Show photo', 'ghost', () =>
        showFile(`/api/admin/shadows/${s.id}/photo`, 'photo', holder)));
    }

    (s.documents || []).forEach((doc, index) => {
      const title = doc.certificateName || doc.kind;
      const details = [doc.issuer, doc.number].filter(Boolean).join(' | ');
      actions.append(button(details ? `${title} (${details})` : title, 'ghost', () =>
        showFile(`/api/admin/shadows/${s.id}/documents/${index}`, doc.fileName, holder)));
    });

    if (s.status !== 'Approved' && s.status !== 1) {
      actions.append(button('Approve', '', () => review(s.id, 'approve')));
    }
    if (s.status !== 'Rejected' && s.status !== 2) {
      actions.append(button('Reject', 'red', () => {
        const reason = window.prompt('Reason (optional):') || '';
        review(s.id, 'reject', { reason });
      }));
    }

    card.append(actions, holder);
    list.append(card);
  }
}

async function load() {
  say('');
  const response = await call(`/api/admin/shadows?status=${status}`);
  if (response.status === 401) {
    say('Wrong password.');
    return;
  }
  if (response.status === 429) {
    say('Too many tries. Wait a minute.');
    return;
  }
  if (!response.ok) {
    say('The server could not answer. Check that the admin password is set.');
    return;
  }
  const data = await response.json();
  $('tabs').hidden = false;
  render(data.shadows || []);
}

$('load').addEventListener('click', () => {
  password = $('password').value;
  load();
});

$('password').addEventListener('keydown', (event) => {
  if (event.key === 'Enter') $('load').click();
});

for (const tab of document.querySelectorAll('#tabs button')) {
  tab.addEventListener('click', () => {
    status = tab.dataset.status;
    for (const other of document.querySelectorAll('#tabs button')) {
      other.className = other === tab ? 'on' : 'ghost';
    }
    load();
  });
}
