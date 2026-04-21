/* ═══════════════════════════════════════════════════════════════════
   CV Builder Pro — app.js
   Architecture:
     · state{}       – single source of truth for template/theme/photo
     · collectData() – reads ALL form fields into a plain JS object
     · renderPreview()– converts that object to preview HTML (no reload)
     · Event delegation on #formPanel catches dynamic elements too
═══════════════════════════════════════════════════════════════════ */

'use strict';

// ── App state ────────────────────────────────────────────────────────────────
const state = {
  template: 'modern',   // 'modern' | 'classic'
  theme:    'blue',     // 'blue' | 'dark' | 'purple' | 'emerald'
  isPremium: false,
  plan:     'free',     // 'free' | 'pro' | 'business'
  profilePic: null,     // compressed base64 data-URL or null
};

// ── Monetization constants ────────────────────────────────────────────────────
const FREE_DAILY_LIMIT = 3; // free exports per day before paywall
const STORAGE_KEY_PREMIUM = 'cvbPremium_v1';
const STORAGE_KEY_PLAN    = 'cvbPlan_v1';    // 'pro' | 'business'
const STORAGE_KEY_EXPORTS = 'cvbExports_v1';

// Colour palette per theme (mirrors PdfService on the backend)
const THEMES = {
  blue:    { accent: '#1d4ed8', light: '#dbeafe', track: '#4272df', text: '#ffffff' },
  dark:    { accent: '#0f172a', light: '#e2e8f0', track: '#334155', text: '#f1f5f9' },
  purple:  { accent: '#7c3aed', light: '#ede9fe', track: '#9d6cf5', text: '#ffffff' },
  emerald: { accent: '#059669', light: '#d1fae5', track: '#34d399', text: '#ffffff' },
  rose:    { accent: '#e11d48', light: '#ffe4e6', track: '#fb7185', text: '#ffffff' },
  teal:    { accent: '#0891b2', light: '#cffafe', track: '#22d3ee', text: '#ffffff' },
  orange:  { accent: '#ea580c', light: '#ffedd5', track: '#fb923c', text: '#ffffff' },
};

let _eduIdx = 0, _expIdx = 0, _sklIdx = 0; // unique IDs for dynamic entries

// ════════════════════════════════════════════════════════════════════════════
//  INITIALISE
// ════════════════════════════════════════════════════════════════════════════
document.addEventListener('DOMContentLoaded', () => {
  // ── Restore premium status from localStorage (persists across sessions)
  if (localStorage.getItem(STORAGE_KEY_PREMIUM) === '1') {
    state.isPremium = true;
    state.plan = localStorage.getItem(STORAGE_KEY_PLAN) || 'pro';
  }

  // ── Activate real Google AdSense if publisher ID is configured
  initAdsense();

  setupEventDelegation();
  setupTemplateSelector(); // also calls refreshTemplateUnlocks()
  setupThemeSelector();
  setupFormSubmit();
  updateCounterBadge();     // show correct export count on nav

  // Seed with one blank entry each so the form isn't empty
  addEducation();
  addSkill('JavaScript', 90);
  addSkill('Communication', 75);
  addExperience();

  renderPreview();
});

// ════════════════════════════════════════════════════════════════════════════
//  EVENT DELEGATION  — the key fix for the "Preview not working" bug.
//  Because skills/education/experience entries are added dynamically, we
//  listen on a stable ancestor (#formPanel) and let events bubble up.
// ════════════════════════════════════════════════════════════════════════════
function setupEventDelegation() {
  const panel = document.getElementById('formPanel');

  // 'input' fires instantly on text fields, textareas, and range sliders
  panel.addEventListener('input', debounce(renderPreview, 120));

  // 'change' fires on selects and when input loses focus with a changed value
  panel.addEventListener('change', renderPreview);

  // Show live % on skill sliders as the user drags
  panel.addEventListener('input', e => {
    if (e.target.classList.contains('skill-slider')) {
      const entry = e.target.closest('.skill-entry');
      if (entry) entry.querySelector('.skill-pct').textContent = e.target.value + '%';
    }
  });
}

// ════════════════════════════════════════════════════════════════════════════
//  COLLECT FORM DATA  — reads everything from the DOM into a plain object
// ════════════════════════════════════════════════════════════════════════════
function collectData() {
  const val = id => document.getElementById(id)?.value.trim() ?? '';

  const education = [...document.querySelectorAll('.edu-entry')].map(el => ({
    degree:      el.querySelector('.edu-degree')?.value.trim()      ?? '',
    institution: el.querySelector('.edu-inst')?.value.trim()   ?? '',
    years:       el.querySelector('.edu-years')?.value.trim()       ?? '',
  }));

  const skills = [...document.querySelectorAll('.skill-entry')].map(el => ({
    name:  el.querySelector('.skill-name')?.value.trim()  ?? '',
    level: parseInt(el.querySelector('.skill-slider')?.value ?? '80', 10),
  })).filter(s => s.name);

  const experience = [...document.querySelectorAll('.exp-entry')].map(el => ({
    jobTitle:    el.querySelector('.exp-title')?.value.trim()   ?? '',
    company:     el.querySelector('.exp-company')?.value.trim() ?? '',
    period:      el.querySelector('.exp-period')?.value.trim()  ?? '',
    description: el.querySelector('.exp-desc')?.value.trim()   ?? '',
  }));

  return {
    fullName:       val('fullName'),
    email:          val('email'),
    phone:          val('phone'),
    address:        val('address'),
    summary:        val('summary'),
    profilePicture: state.profilePic,
    template:       state.template,
    theme:          state.theme,
    isPremium:      state.isPremium,  // tells backend whether to show watermark
    education,
    skills,
    experience,
  };
}

// ════════════════════════════════════════════════════════════════════════════
//  RENDER PREVIEW  — called on every form change, template switch, or theme
//  switch. Produces HTML and injects it into #previewContent with NO reload.
// ════════════════════════════════════════════════════════════════════════════
function renderPreview() {
  const data = collectData();
  const box  = document.getElementById('previewContent');

  // Use placeholder values so template/theme changes are always visible
  // even before the user fills in the form.
  if (!data.fullName) {
    data.fullName = 'Your Full Name';
    data.email    = data.email    || 'your@email.com';
    data.address  = data.address  || 'City, Country';
    data.summary  = data.summary  || 'A brief professional summary will appear here once you fill in the form above.';
  }

  const builders = {
    classic:   buildClassicPreview,
    executive: buildExecutivePreview,
    minimal:   buildMinimalPreview,
    creative:  buildCreativePreview,
  };
  const fn = builders[data.template] ?? buildModernPreview;
  box.innerHTML = fn(data);
}

// Called from the "Refresh Preview" button and template/theme selectors
function triggerPreview() {
  renderPreview();
  // On mobile, scroll preview into view
  document.querySelector('.preview-panel')
    ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

// ── MODERN template HTML ─────────────────────────────────────────────────────
function buildModernPreview(d) {
  const pal = THEMES[d.theme] ?? THEMES.blue;

  const contacts = [
    d.email   && `<span class="cv-contact-chip">✉ ${x(d.email)}</span>`,
    d.phone   && `<span class="cv-contact-chip">📞 ${x(d.phone)}</span>`,
    d.address && `<span class="cv-contact-chip">📍 ${x(d.address)}</span>`,
  ].filter(Boolean).join('');

  const photoHtml = d.profilePicture
    ? `<img src="${d.profilePicture}" class="cv-photo-preview" alt="Photo" />`
    : '';

  const skillsHtml = d.skills.length ? `
    <div class="cv-section-hd" style="color:${pal.text}">Skills</div>
    ${d.skills.map(s => `
      <div class="cv-skill-row">
        <div class="cv-skill-label" style="color:${pal.text}">
          <span>${x(s.name)}</span><span>${s.level}%</span>
        </div>
        <div class="cv-skill-track">
          <div class="cv-skill-fill" style="width:${s.level}%"></div>
        </div>
      </div>`).join('')}` : '';

  const expHtml = d.experience.filter(j => j.jobTitle).length ? `
    <div class="cv-section-hd" style="color:${pal.accent}">Work Experience</div>
    ${d.experience.filter(j => j.jobTitle).map(j => `
      <div class="cv-job-block">
        <div style="display:flex;justify-content:space-between;align-items:baseline">
          <div class="cv-job-title">${x(j.jobTitle)}</div>
          <div class="cv-job-period">${x(j.period)}</div>
        </div>
        <div class="cv-job-company" style="color:${pal.accent}">${x(j.company)}</div>
        ${j.description ? `<div class="cv-job-desc">${x(j.description)}</div>` : ''}
      </div>`).join('')}` : '';

  const eduHtml = d.education.filter(e => e.degree).length ? `
    <div class="cv-section-hd" style="color:${pal.accent}">Education</div>
    ${d.education.filter(e => e.degree).map(e => `
      <div class="cv-edu-block">
        <div style="display:flex;justify-content:space-between">
          <div class="cv-edu-degree">${x(e.degree)}</div>
          <div class="cv-edu-years">${x(e.years)}</div>
        </div>
        <div class="cv-edu-inst" style="color:${pal.accent}">${x(e.institution)}</div>
      </div>`).join('')}` : '';

  const summaryHtml = d.summary ? `
    <div class="cv-section-hd" style="color:${pal.accent}">Profile Summary</div>
    <div style="font-size:9.5px;color:#475569;line-height:1.55;margin-bottom:10px">${x(d.summary)}</div>` : '';

  return `
    <div class="cv-modern">
      <div class="cv-sidebar" style="background:${pal.accent};color:${pal.text}">
        ${photoHtml}
        <div class="cv-name-big" style="color:${pal.text}">${x(d.fullName)}</div>
        <div class="cv-contacts-row" style="flex-direction:column;gap:6px;margin-top:8px">
          ${contacts.replace(/class="cv-contact-chip"/g, `class="cv-contact-chip" style="color:${pal.text}"`)}
        </div>
        ${skillsHtml}
      </div>
      <div class="cv-main" style="background:#fff">
        ${summaryHtml}${expHtml}${eduHtml}
      </div>
    </div>`;
}

// ── CLASSIC template HTML ─────────────────────────────────────────────────────
function buildClassicPreview(d) {
  const pal = THEMES[d.theme] ?? THEMES.blue;

  const photoHtml = d.profilePicture
    ? `<img src="${d.profilePicture}" class="cv-photo-classic" alt="Photo" />`
    : '';

  const contacts = [
    d.email   && `<span class="cv-contact-chip">✉ ${x(d.email)}</span>`,
    d.phone   && `<span class="cv-contact-chip">📞 ${x(d.phone)}</span>`,
    d.address && `<span class="cv-contact-chip">📍 ${x(d.address)}</span>`,
  ].filter(Boolean).join('');

  const expHtml = d.experience.filter(j => j.jobTitle).length ? `
    <div class="cv-section-hd" style="color:${pal.accent}">Work Experience</div>
    ${d.experience.filter(j => j.jobTitle).map(j => `
      <div class="cv-job-block">
        <div style="display:flex;justify-content:space-between;align-items:baseline">
          <div class="cv-job-title">${x(j.jobTitle)}</div>
          <div class="cv-job-period">${x(j.period)}</div>
        </div>
        <div class="cv-job-company" style="color:${pal.accent}">${x(j.company)}</div>
        ${j.description ? `<div class="cv-job-desc">${x(j.description)}</div>` : ''}
      </div>`).join('')}` : '';

  const eduHtml = d.education.filter(e => e.degree).length ? `
    <div class="cv-section-hd" style="color:${pal.accent}">Education</div>
    ${d.education.filter(e => e.degree).map(e => `
      <div class="cv-edu-block">
        <div style="display:flex;justify-content:space-between">
          <div class="cv-edu-degree">${x(e.degree)}</div>
          <div class="cv-edu-years">${x(e.years)}</div>
        </div>
        <div class="cv-edu-inst" style="color:${pal.accent}">${x(e.institution)}</div>
      </div>`).join('')}` : '';

  const skillsHtml = d.skills.length ? `
    <div class="cv-section-hd" style="color:${pal.accent}">Skills</div>
    <div class="cv-skill-chips">
      ${d.skills.map(s => `
        <div>
          <div class="cv-skill-chip" style="background:${pal.light};color:${pal.accent}">${x(s.name)}</div>
          <div class="cv-skill-chip-bar" style="background:#e2e8f0">
            <div style="height:100%;width:${s.level}%;background:${pal.accent};border-radius:1px"></div>
          </div>
        </div>`).join('')}
    </div>` : '';

  const summaryHtml = d.summary ? `
    <div class="cv-section-hd" style="color:${pal.accent}">Profile Summary</div>
    <div style="font-size:9.5px;color:#475569;line-height:1.55;margin-bottom:10px">${x(d.summary)}</div>` : '';

  return `
    <div class="cv-classic">
      <div class="cv-header" style="background:${pal.accent}">
        <div style="display:flex;align-items:center">
          ${photoHtml}
          <div>
            <div class="cv-name-med" style="color:${pal.text}">${x(d.fullName)}</div>
            <div class="cv-contacts-row" style="color:${pal.text}">${contacts}</div>
          </div>
        </div>
      </div>
      <div class="cv-body" style="background:#fff">
        ${summaryHtml}${expHtml}${eduHtml}${skillsHtml}
      </div>
    </div>`;
}

// ── EXECUTIVE template HTML (PRO) ────────────────────────────────────────────
function buildExecutivePreview(d) {
  const pal = THEMES[d.theme] ?? THEMES.blue;

  const photoHtml = d.profilePicture
    ? `<img src="${d.profilePicture}" class="cv-photo-classic" alt="Photo" style="margin-left:12px" />`
    : '';

  const contacts = [
    d.email   && `✉ ${x(d.email)}`,
    d.phone   && `☎ ${x(d.phone)}`,
    d.address && `⌖ ${x(d.address)}`,
  ].filter(Boolean).map(t => `<span style="margin-right:16px;font-size:9px;opacity:.85;color:${pal.text}">${t}</span>`).join('');

  const skillsHtml = d.skills.length ? `
    <div style="font-size:8px;font-weight:800;letter-spacing:.1em;color:${pal.accent};margin-bottom:6px">SKILLS</div>
    <div style="height:2px;background:${pal.accent};margin-bottom:12px"></div>
    ${d.skills.map(s => `
      <div style="margin-bottom:9px">
        <div style="display:flex;justify-content:space-between;font-size:9px;margin-bottom:3px">
          <span>${x(s.name)}</span><span style="color:#64748b">${s.level}%</span>
        </div>
        <div style="height:4px;background:#d1d5db;border-radius:2px;overflow:hidden">
          <div style="height:100%;width:${s.level}%;background:${pal.accent};border-radius:2px"></div>
        </div>
      </div>`).join('')}` : '';

  const expHtml = d.experience.filter(j => j.jobTitle).length ? `
    <div class="cv-exec-section-hd" style="color:${pal.accent}">WORK EXPERIENCE</div>
    ${d.experience.filter(j => j.jobTitle).map(j => `
      <div class="cv-job-block">
        <div style="display:flex;justify-content:space-between;align-items:baseline">
          <div class="cv-job-title">${x(j.jobTitle)}</div>
          <div class="cv-job-period">${x(j.period)}</div>
        </div>
        <div class="cv-job-company" style="color:${pal.accent}">${x(j.company)}</div>
        ${j.description ? `<div class="cv-job-desc">${x(j.description)}</div>` : ''}
      </div>`).join('')}` : '';

  const eduHtml = d.education.filter(e => e.degree).length ? `
    <div class="cv-exec-section-hd" style="color:${pal.accent}">EDUCATION</div>
    ${d.education.filter(e => e.degree).map(e => `
      <div class="cv-edu-block">
        <div style="display:flex;justify-content:space-between">
          <div class="cv-edu-degree">${x(e.degree)}</div>
          <div class="cv-edu-years">${x(e.years)}</div>
        </div>
        <div class="cv-edu-inst" style="color:${pal.accent}">${x(e.institution)}</div>
      </div>`).join('')}` : '';

  const summaryHtml = d.summary ? `
    <div class="cv-exec-section-hd" style="color:${pal.accent}">PROFILE SUMMARY</div>
    <div style="font-size:9.5px;color:#475569;line-height:1.55;margin-bottom:12px">${x(d.summary)}</div>` : '';

  return `
    <div class="cv-executive">
      <div class="cv-exec-header" style="background:${pal.accent}">
        <div style="display:flex;align-items:center;justify-content:space-between">
          <div>
            <div class="cv-name-big" style="color:${pal.text}">${x(d.fullName)}</div>
            <div style="margin-top:8px">${contacts}</div>
          </div>
          ${photoHtml}
        </div>
      </div>
      <div class="cv-exec-body">
        <div class="cv-exec-sidebar" style="background:${pal.light}">${skillsHtml}</div>
        <div class="cv-exec-main">${summaryHtml}${expHtml}${eduHtml}</div>
      </div>
    </div>`;
}

// ── MINIMAL template HTML (PRO) ──────────────────────────────────────────────
function buildMinimalPreview(d) {
  const pal = THEMES[d.theme] ?? THEMES.blue;

  const photoHtml = d.profilePicture
    ? `<img src="${d.profilePicture}" style="width:64px;height:64px;object-fit:cover;border:2px solid ${pal.accent};margin-bottom:14px;display:block" alt="Photo" />`
    : '';

  const contacts = [d.email && `✉ ${x(d.email)}`, d.phone && `☎ ${x(d.phone)}`, d.address && `⌖ ${x(d.address)}`]
    .filter(Boolean).join(' &nbsp;·&nbsp; ');

  const expHtml = d.experience.filter(j => j.jobTitle).length ? `
    <div class="cv-min-section-hd" style="color:${pal.accent}">WORK EXPERIENCE</div>
    ${d.experience.filter(j => j.jobTitle).map(j => `
      <div class="cv-job-block">
        <div style="display:flex;justify-content:space-between;align-items:baseline">
          <div class="cv-job-title">${x(j.jobTitle)}</div>
          <div class="cv-job-period">${x(j.period)}</div>
        </div>
        <div class="cv-job-company" style="color:${pal.accent}">${x(j.company)}</div>
        ${j.description ? `<div class="cv-job-desc">${x(j.description)}</div>` : ''}
      </div>`).join('')}` : '';

  const eduHtml = d.education.filter(e => e.degree).length ? `
    <div class="cv-min-section-hd" style="color:${pal.accent}">EDUCATION</div>
    ${d.education.filter(e => e.degree).map(e => `
      <div class="cv-edu-block">
        <div style="display:flex;justify-content:space-between">
          <div class="cv-edu-degree">${x(e.degree)}</div>
          <div class="cv-edu-years">${x(e.years)}</div>
        </div>
        <div class="cv-edu-inst" style="color:${pal.accent}">${x(e.institution)}</div>
      </div>`).join('')}` : '';

  const skillsHtml = d.skills.length ? `
    <div class="cv-min-section-hd" style="color:${pal.accent}">SKILLS</div>
    <div style="display:flex;flex-wrap:wrap;gap:6px;margin-bottom:12px">
      ${d.skills.map(s => `
        <span style="padding:3px 10px;border-radius:4px;font-size:9px;font-weight:600;
          background:${pal.light};color:${pal.accent}">${x(s.name)} ${s.level}%</span>`).join('')}
    </div>` : '';

  const summaryHtml = d.summary ? `
    <div class="cv-min-section-hd" style="color:${pal.accent}">PROFILE SUMMARY</div>
    <div style="font-size:9.5px;color:#475569;line-height:1.55;margin-bottom:12px">${x(d.summary)}</div>` : '';

  return `
    <div class="cv-minimal">
      <div style="border-bottom:3px solid ${pal.accent};padding-bottom:10px;margin-bottom:10px">
        <div style="font-size:20px;font-weight:800;color:#0f172a;line-height:1.2">${x(d.fullName)}</div>
      </div>
      ${photoHtml}
      <div style="font-size:9px;color:#64748b;margin-bottom:16px">${contacts}</div>
      ${summaryHtml}${expHtml}${eduHtml}${skillsHtml}
    </div>`;
}

// ── CREATIVE template HTML (BUSINESS) ────────────────────────────────────────
function buildCreativePreview(d) {
  const pal    = THEMES[d.theme] ?? THEMES.blue;
  const darkBg = '#0f172a';
  const dimTxt = '#94a3b8';

  const photoHtml = d.profilePicture
    ? `<img src="${d.profilePicture}" style="width:72px;height:72px;object-fit:cover;border:3px solid #fff;display:block;margin:0 auto 12px" alt="Photo" />`
    : '';

  const skillsHtml = d.skills.length ? `
    <div style="font-size:8px;font-weight:800;color:${pal.accent};margin:14px 0 5px">SKILLS</div>
    <div style="height:1.5px;background:${pal.accent};margin-bottom:11px"></div>
    ${d.skills.map(s => `
      <div style="margin-bottom:9px">
        <div style="display:flex;justify-content:space-between;font-size:9px;margin-bottom:3px">
          <span style="color:#f1f5f9">${x(s.name)}</span>
          <span style="color:${dimTxt}">${s.level}%</span>
        </div>
        <div style="height:4px;background:#1e293b;border-radius:2px;overflow:hidden">
          <div style="height:100%;width:${s.level}%;background:${pal.accent};border-radius:2px"></div>
        </div>
      </div>`).join('')}` : '';

  const contacts = [
    d.email   && `<div style="margin-bottom:8px"><div style="font-size:7px;font-weight:700;color:${dimTxt};letter-spacing:.08em">EMAIL</div><div style="font-size:8.5px;color:#f1f5f9">${x(d.email)}</div></div>`,
    d.phone   && `<div style="margin-bottom:8px"><div style="font-size:7px;font-weight:700;color:${dimTxt};letter-spacing:.08em">PHONE</div><div style="font-size:8.5px;color:#f1f5f9">${x(d.phone)}</div></div>`,
    d.address && `<div style="margin-bottom:14px"><div style="font-size:7px;font-weight:700;color:${dimTxt};letter-spacing:.08em">LOCATION</div><div style="font-size:8.5px;color:#f1f5f9">${x(d.address)}</div></div>`,
  ].filter(Boolean).join('');

  const expHtml = d.experience.filter(j => j.jobTitle).length ? `
    <div class="cv-creative-section-hd" style="border-left:4px solid ${pal.accent}">WORK EXPERIENCE</div>
    ${d.experience.filter(j => j.jobTitle).map(j => `
      <div class="cv-job-block">
        <div style="display:flex;justify-content:space-between;align-items:baseline">
          <div class="cv-job-title">${x(j.jobTitle)}</div>
          <div class="cv-job-period">${x(j.period)}</div>
        </div>
        <div class="cv-job-company" style="color:${pal.accent}">${x(j.company)}</div>
        ${j.description ? `<div class="cv-job-desc">${x(j.description)}</div>` : ''}
      </div>`).join('')}` : '';

  const eduHtml = d.education.filter(e => e.degree).length ? `
    <div class="cv-creative-section-hd" style="border-left:4px solid ${pal.accent}">EDUCATION</div>
    ${d.education.filter(e => e.degree).map(e => `
      <div class="cv-edu-block">
        <div style="display:flex;justify-content:space-between">
          <div class="cv-edu-degree">${x(e.degree)}</div>
          <div class="cv-edu-years">${x(e.years)}</div>
        </div>
        <div class="cv-edu-inst" style="color:${pal.accent}">${x(e.institution)}</div>
      </div>`).join('')}` : '';

  const summaryHtml = d.summary ? `
    <div class="cv-creative-section-hd" style="border-left:4px solid ${pal.accent}">PROFILE SUMMARY</div>
    <div style="font-size:9.5px;color:#475569;line-height:1.55;margin-bottom:12px">${x(d.summary)}</div>` : '';

  return `
    <div class="cv-creative">
      <div class="cv-creative-sidebar" style="background:${darkBg}">
        <div style="height:7px;background:${pal.accent}"></div>
        <div style="padding:18px 16px">
          ${photoHtml}
          <div style="font-size:14px;font-weight:800;color:#f1f5f9;line-height:1.2;margin-bottom:4px">${x(d.fullName)}</div>
          <div style="height:2px;background:${pal.accent};margin-bottom:14px"></div>
          ${contacts}
          ${skillsHtml}
        </div>
      </div>
      <div class="cv-creative-main">${summaryHtml}${expHtml}${eduHtml}</div>
    </div>`;
}

// ════════════════════════════════════════════════════════════════════════════
//  DYNAMIC ENTRY BUILDERS
// ════════════════════════════════════════════════════════════════════════════

function addEducation() {
  const id = `edu-${++_eduIdx}`;
  const div = document.createElement('div');
  div.className = 'entry-card edu-entry';
  div.id = id;
  div.innerHTML = `
    <button type="button" class="btn-remove" onclick="removeEntry('${id}')" title="Remove">
      <i class="bi bi-x-circle-fill"></i>
    </button>
    <div class="row g-2">
      <div class="col-12">
        <label class="flabel">Degree / Qualification</label>
        <input type="text" class="form-control form-control-sm edu-degree" placeholder="e.g. BSc Computer Science" />
      </div>
      <div class="col-8">
        <label class="flabel">Institution</label>
        <input type="text" class="form-control form-control-sm edu-inst" placeholder="e.g. Oxford University" />
      </div>
      <div class="col-4">
        <label class="flabel">Years</label>
        <input type="text" class="form-control form-control-sm edu-years" placeholder="2019–2023" />
      </div>
    </div>`;
  document.getElementById('educationContainer').appendChild(div);
}

function addExperience() {
  const id = `exp-${++_expIdx}`;
  const div = document.createElement('div');
  div.className = 'entry-card exp-entry';
  div.id = id;
  div.innerHTML = `
    <button type="button" class="btn-remove" onclick="removeEntry('${id}')" title="Remove">
      <i class="bi bi-x-circle-fill"></i>
    </button>
    <div class="row g-2">
      <div class="col-sm-7">
        <label class="flabel">Job Title</label>
        <input type="text" class="form-control form-control-sm exp-title" placeholder="e.g. Senior Developer" />
      </div>
      <div class="col-sm-5">
        <label class="flabel">Period</label>
        <input type="text" class="form-control form-control-sm exp-period" placeholder="Jan 2021 – Present" />
      </div>
      <div class="col-12">
        <label class="flabel">Company</label>
        <input type="text" class="form-control form-control-sm exp-company" placeholder="e.g. Google, Inc." />
      </div>
      <div class="col-12">
        <label class="flabel">Description</label>
        <textarea class="form-control form-control-sm exp-desc" rows="2"
          placeholder="Key responsibilities and achievements..."></textarea>
      </div>
    </div>`;
  document.getElementById('experienceContainer').appendChild(div);
}

function addSkill(name = '', level = 80) {
  const id = `skl-${++_sklIdx}`;
  const div = document.createElement('div');
  div.className = 'entry-card skill-entry';
  div.id = id;
  div.innerHTML = `
    <div class="skill-row-top">
      <input type="text" class="form-control form-control-sm skill-name"
             placeholder="e.g. JavaScript" value="${x(name)}" />
      <button type="button" class="btn-remove" onclick="removeEntry('${id}')" title="Remove">
        <i class="bi bi-x-circle-fill"></i>
      </button>
    </div>
    <div class="skill-slider-row">
      <input type="range" class="skill-slider" min="10" max="100" step="5" value="${level}" />
      <span class="skill-pct">${level}%</span>
    </div>`;
  document.getElementById('skillsContainer').appendChild(div);
}

function removeEntry(id) {
  document.getElementById(id)?.remove();
  renderPreview(); // re-render immediately after removal
}

// ════════════════════════════════════════════════════════════════════════════
//  TEMPLATE & THEME SELECTORS
// ════════════════════════════════════════════════════════════════════════════
function setupTemplateSelector() {
  document.querySelectorAll('.tpl-card').forEach(card => {
    const requiredPlan = card.dataset.plan; // 'pro' | 'business' | undefined
    const radio = card.querySelector('input[name="template"]');

    card.addEventListener('click', e => {
      if (!canUsePlan(requiredPlan)) {
        e.preventDefault();
        openPremiumModal();
      }
    });

    if (radio) {
      radio.addEventListener('change', () => {
        if (!canUsePlan(requiredPlan)) { radio.checked = false; return; }
        state.template = radio.value;
        renderPreview();
      });
    }
  });

  refreshTemplateUnlocks();
}

/** Returns true if user's current plan satisfies the required plan. */
function canUsePlan(requiredPlan) {
  if (!requiredPlan) return true;                          // free template
  if (requiredPlan === 'pro') return state.isPremium;     // pro OR business
  if (requiredPlan === 'business') return state.plan === 'business';
  return false;
}

/** Enable/disable template cards based on current plan. */
function refreshTemplateUnlocks() {
  document.querySelectorAll('.tpl-card[data-plan]').forEach(card => {
    const unlocked = canUsePlan(card.dataset.plan);
    card.classList.toggle('locked', !unlocked);

    const radio = card.querySelector('input[name="template"]');
    if (radio) radio.disabled = !unlocked;

    // Update badge: show lock icon only when locked
    const badge = card.querySelector('.tpl-badge');
    if (badge) {
      const lockIcon = badge.querySelector('.bi-lock-fill');
      if (lockIcon) lockIcon.style.display = unlocked ? 'none' : '';
    }
  });
}

function setupThemeSelector() {
  document.querySelectorAll('.tdot').forEach(dot => {
    dot.addEventListener('click', () => {
      state.theme = dot.dataset.theme;
      document.querySelectorAll('.tdot').forEach(d => d.classList.remove('active'));
      dot.classList.add('active');
      renderPreview();
    });
  });
}

// ════════════════════════════════════════════════════════════════════════════
//  PROFILE PHOTO UPLOAD
//  Compresses to max 200×200 px before storing as base64 to keep JSON small.
// ════════════════════════════════════════════════════════════════════════════
function handlePhotoSelect(input) {
  if (!input.files?.[0]) return;
  loadAndCompressImage(input.files[0]);
}

function handlePhotoDrop(event) {
  event.preventDefault();
  const file = event.dataTransfer.files?.[0];
  if (file && file.type.startsWith('image/')) loadAndCompressImage(file);
}

function loadAndCompressImage(file) {
  if (file.size > 2 * 1024 * 1024) {
    showStatus('Photo must be under 2 MB.', 'error');
    return;
  }
  const reader = new FileReader();
  reader.onload = e => {
    const img = new Image();
    img.onload = () => {
      // Resize to max 200×200 via canvas
      const MAX = 200;
      const scale = Math.min(MAX / img.width, MAX / img.height, 1);
      const canvas = document.createElement('canvas');
      canvas.width  = Math.round(img.width  * scale);
      canvas.height = Math.round(img.height * scale);
      canvas.getContext('2d').drawImage(img, 0, 0, canvas.width, canvas.height);
      const compressed = canvas.toDataURL('image/jpeg', 0.85);

      state.profilePic = compressed;

      // Show preview in the upload zone
      const previewImg = document.getElementById('photoPreviewImg');
      const placeholder = document.getElementById('photoPlaceholder');
      const removeBtn   = document.getElementById('photoRemoveBtn');
      previewImg.src = compressed;
      previewImg.style.display = 'block';
      placeholder.style.display = 'none';
      removeBtn.style.display = 'flex';

      renderPreview();
    };
    img.src = e.target.result;
  };
  reader.readAsDataURL(file);
}

function removePhoto(event) {
  event.stopPropagation(); // don't re-open file picker
  state.profilePic = null;
  const previewImg  = document.getElementById('photoPreviewImg');
  const placeholder = document.getElementById('photoPlaceholder');
  const removeBtn   = document.getElementById('photoRemoveBtn');
  previewImg.src = '';
  previewImg.style.display = 'none';
  placeholder.style.display = 'flex';
  removeBtn.style.display = 'none';
  document.getElementById('photoInput').value = '';
  renderPreview();
}

// ════════════════════════════════════════════════════════════════════════════
//  EXPORT COUNTER  — daily free limit enforced via localStorage
// ════════════════════════════════════════════════════════════════════════════

/** Returns { date, count } from localStorage, resetting if it's a new day. */
function getExportState() {
  const today = new Date().toDateString();
  try {
    const stored = JSON.parse(localStorage.getItem(STORAGE_KEY_EXPORTS) || '{}');
    if (stored.date === today) return stored;
  } catch (_) {}
  return { date: new Date().toDateString(), count: 0 };
}

/** Returns how many exports remain today (Infinity for premium). */
function exportsRemaining() {
  if (state.isPremium) return Infinity;
  return Math.max(0, FREE_DAILY_LIMIT - getExportState().count);
}

/** Increments today's export count in localStorage. */
function recordExport() {
  const s = getExportState();
  s.count = (s.count || 0) + 1;
  localStorage.setItem(STORAGE_KEY_EXPORTS, JSON.stringify(s));
  updateCounterBadge();
}

/** Updates the navbar counter badge text and colour. */
function updateCounterBadge() {
  const badge = document.getElementById('exportCounter');
  if (!badge) return;

  if (state.isPremium) {
    const planLabel = state.plan === 'business' ? 'Business' : 'Pro';
    badge.innerHTML = `<i class="bi bi-stars me-1"></i>${planLabel} — Unlimited`;
    badge.className = 'counter-badge premium';
    return;
  }

  const left = exportsRemaining();
  badge.textContent = `${left} free export${left === 1 ? '' : 's'} today`;
  if (left === 0) {
    badge.className = 'counter-badge warning';
    badge.textContent = '⚠ Daily limit reached';
  } else if (left === 1) {
    badge.className = 'counter-badge warning';
  } else {
    badge.className = 'counter-badge';
  }
}

// ════════════════════════════════════════════════════════════════════════════
//  FORM SUBMIT → Generate PDF
// ════════════════════════════════════════════════════════════════════════════
function setupFormSubmit() {
  document.getElementById('cvForm').addEventListener('submit', async e => {
    e.preventDefault();
    const data = collectData();

    if (!data.fullName) {
      showStatus('Please enter your Full Name first.', 'error');
      document.getElementById('fullName').focus();
      return;
    }

    // ── Free-tier export gate ───────────────────────────────────────
    if (exportsRemaining() <= 0) {
      openPremiumModal('limit');
      return;
    }

    setBusy(true);
    showStatus('', 'hide');

    try {
      const res = await fetch('/api/cv/generate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({ error: 'Server error.' }));
        throw new Error(err.error ?? 'Unknown error from server.');
      }

      // Trigger browser file download from the blob response
      const blob = await res.blob();
      const url  = URL.createObjectURL(blob);
      const a    = document.createElement('a');
      const disp = res.headers.get('Content-Disposition') ?? '';
      const match = disp.match(/filename="?([^";\n]+)"?/i);
      a.href     = url;
      a.download = match ? match[1] : 'CV.pdf';
      a.click();
      URL.revokeObjectURL(url);

      // Record the export and update counter
      recordExport();

      const left = exportsRemaining();
      const msg  = state.isPremium
        ? 'PDF downloaded! (Premium — unlimited exports)'
        : `PDF downloaded! ${left} free export${left === 1 ? '' : 's'} remaining today.`;
      showStatus(msg, 'success');
    } catch (err) {
      showStatus(`Error: ${err.message}`, 'error');
    } finally {
      setBusy(false);
    }
  });
}

function setBusy(busy) {
  const btn  = document.getElementById('generateBtn');
  const side = document.getElementById('generateBtnSide');
  btn.disabled = side.disabled = busy;
  const label = busy
    ? `<span class="spinner-border spinner-border-sm me-2"></span>Generating…`
    : `<i class="bi bi-file-earmark-arrow-down-fill me-2"></i>Download PDF`;
  btn.innerHTML = side.innerHTML = label;
}

// ════════════════════════════════════════════════════════════════════════════
//  PREMIUM MODAL
// ════════════════════════════════════════════════════════════════════════════
function openPremiumModal(reason) {
  // Update modal header based on why it was opened
  if (reason === 'limit') {
    document.getElementById('pmIcon').innerHTML  = '<i class="bi bi-lock-fill"></i>';
    document.getElementById('pmTitle').textContent = 'Daily Limit Reached';
    document.getElementById('pmSub').textContent   =
      `Free plan: ${FREE_DAILY_LIMIT} PDF exports per day. Upgrade for unlimited access.`;
  } else {
    document.getElementById('pmIcon').innerHTML  = '<i class="bi bi-stars"></i>';
    document.getElementById('pmTitle').textContent = 'Unlock Premium';
    document.getElementById('pmSub').textContent   =
      'Get Executive template, unlimited exports & remove the free-plan watermark.';
  }
  document.getElementById('premiumModalBackdrop').classList.add('open');
  showStep1();
}

function closePremiumModal(event) {
  // Close when clicking backdrop, not the modal card itself
  if (event && event.target !== document.getElementById('premiumModalBackdrop')) return;
  document.getElementById('premiumModalBackdrop').classList.remove('open');
}

function showStep1() {
  document.getElementById('pmStep1').style.display = '';
  document.getElementById('pmStep2').style.display = 'none';
  document.getElementById('pmStep3').style.display = 'none';
}

// plan = 'pro' | 'business', priceLabel = '29.000₫'
let _currentPlan = 'pro';

function showPaymentForm(plan, priceLabel) {
  _currentPlan = plan;
  const names = { pro: 'Pro', business: 'Business' };
  document.getElementById('pmPlanTitle').textContent = `Nâng cấp lên ${names[plan] ?? plan}`;
  document.getElementById('pmPlanPrice').textContent = `${priceLabel} — thanh toán một lần`;
  document.getElementById('pmStep1').style.display = 'none';
  document.getElementById('pmStep2').style.display = '';
  document.getElementById('pmStep3').style.display = 'none';
  document.getElementById('pmPayError').style.display = 'none';
}

// ── Reset the pay button to its idle state ───────────────────────────────────
function resetPayBtn() {
  const btn   = document.getElementById('pmPayBtn');
  const errEl = document.getElementById('pmPayError');
  if (btn) {
    btn.disabled  = false;
    btn.innerHTML = '<i class="bi bi-box-arrow-up-right me-2"></i>Thanh toán qua PayOS';
  }
  if (errEl) errEl.style.display = 'none';
}

// When user navigates BACK from PayOS (browser back-button), the browser
// restores the page from bfcache — button is still spinning.  pageshow fires
// with event.persisted = true in that case, so we reset the button.
window.addEventListener('pageshow', event => {
  if (event.persisted) resetPayBtn();
});

async function startPayOSPayment() {
  const btn   = document.getElementById('pmPayBtn');
  const errEl = document.getElementById('pmPayError');
  errEl.style.display = 'none';
  btn.disabled  = true;
  btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Đang tạo đơn hàng…';

  try {
    const res  = await fetch('/api/payment/create-link', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ plan: _currentPlan }),
    });
    const data = await res.json();

    if (!res.ok || !data.checkoutUrl)
      throw new Error(data.error ?? 'Không thể tạo đơn hàng. Vui lòng thử lại.');

    // Store plan in sessionStorage so payment-success.html knows which plan to activate
    sessionStorage.setItem('cvbPaymentPlan', _currentPlan);

    // Redirect to PayOS — after this, bfcache may restore the page on Back.
    // The pageshow listener above will reset the button when that happens.
    window.location.href = data.checkoutUrl;

  } catch (err) {
    errEl.textContent = err.message;
    errEl.style.display = 'block';
    resetPayBtn();
  }
}

function activatePremium(plan) {
  const activatedPlan = plan || _currentPlan || 'pro';
  state.isPremium = true;
  state.plan = activatedPlan;

  // Persist so premium survives page refresh
  localStorage.setItem(STORAGE_KEY_PREMIUM, '1');
  localStorage.setItem(STORAGE_KEY_PLAN, activatedPlan);

  document.getElementById('premiumModalBackdrop').classList.remove('open');

  // Unlock premium templates visually
  document.querySelectorAll('.tpl-card.locked').forEach(card => {
    card.classList.remove('locked');
    card.style.opacity = '1';
    card.querySelector('.tpl-badge').textContent = activatedPlan === 'business' ? 'BUSINESS' : 'PRO';
  });

  refreshTemplateUnlocks();
  updateCounterBadge();
  const planLabel = activatedPlan === 'business' ? 'Business' : 'Pro';
  showStatus(`🎉 ${planLabel} activated! Unlimited exports, no watermark, all templates unlocked.`, 'success');
}


// ════════════════════════════════════════════════════════════════════════════
//  UTILITIES
// ════════════════════════════════════════════════════════════════════════════

/** HTML-escape to prevent XSS in the preview HTML strings. */
function x(str) {
  if (!str) return '';
  return String(str)
    .replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')
    .replace(/"/g,'&quot;');
}

function showStatus(msg, type) {
  const el = document.getElementById('statusMsg');
  if (!msg || type === 'hide') { el.style.display = 'none'; return; }
  el.className = `status-msg ${type}`;
  el.textContent = msg;
  el.style.display = 'block';
  if (type === 'success') setTimeout(() => (el.style.display = 'none'), 4000);
}

/** Simple debounce — avoids hammering renderPreview on every keystroke. */
function debounce(fn, ms) {
  let t;
  return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms); };
}

// ════════════════════════════════════════════════════════════════════════════
//  GOOGLE ADSENSE LOADER
//  Reads window.ADSENSE_CONFIG from adsense-config.js.
//  If publisherId is set → injects real AdSense script + ins tags.
//  If empty → keeps the demo ad placeholders (no change).
// ════════════════════════════════════════════════════════════════════════════
function initAdsense() {
  const cfg = window.ADSENSE_CONFIG;

  // Nothing to do if publisher ID not filled in yet
  if (!cfg?.publisherId || !cfg.publisherId.startsWith('ca-pub-')) return;

  // 1. Inject the AdSense <script> into <head> (required by Google)
  const script = document.createElement('script');
  script.async = true;
  script.src   = `https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client=${cfg.publisherId}`;
  script.crossOrigin = 'anonymous';
  document.head.appendChild(script);

  // 2. Replace each demo div with a real <ins> tag once the script loads
  script.onload = () => {
    activateAdSlot('adSlotTop',     cfg.slots.topBanner,    728, 90,  cfg);
    activateAdSlot('adSlotSidebar', cfg.slots.sidebarBox,   300, 250, cfg);
    activateAdSlot('adSlotBottom',  cfg.slots.bottomBanner, 728, 90,  cfg);
  };
}

/**
 * Replaces a demo ad div with a real Google AdSense <ins> element.
 * @param {string} containerId - ID of the parent wrapper div
 * @param {string} slot        - AdSense ad slot ID
 * @param {number} w           - Default ad width (px)
 * @param {number} h           - Default ad height (px)
 * @param {object} cfg         - Full ADSENSE_CONFIG object
 */
function activateAdSlot(containerId, slot, w, h, cfg) {
  if (!slot) return; // slot ID not filled in — keep demo ad

  const container = document.getElementById(containerId);
  if (!container) return;

  // Build the <ins> tag Google requires
  const ins = document.createElement('ins');
  ins.className = 'adsbygoogle';

  if (cfg.responsive) {
    // Responsive ad — fills the container width automatically
    ins.style.cssText = 'display:block';
    ins.dataset.adFormat = 'auto';
    ins.dataset.fullWidthResponsive = 'true';
  } else {
    // Fixed-size ad
    ins.style.cssText = `display:inline-block;width:${w}px;height:${h}px`;
  }

  ins.dataset.adClient = cfg.publisherId;
  ins.dataset.adSlot   = slot;

  // Swap demo content for the real ad
  container.innerHTML = '';
  container.appendChild(ins);

  // Tell AdSense to fill this slot
  (window.adsbygoogle = window.adsbygoogle || []).push({});
}
