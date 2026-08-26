
(function () {
  var toggle = document.getElementById('navToggle');
  var sidebar = document.getElementById('sidebar');
  var backdrop = document.getElementById('backdrop');
  if (!toggle || !sidebar || !backdrop) return;

  function closeNav() {
    sidebar.classList.remove('open');
    backdrop.classList.remove('open');
    toggle.setAttribute('aria-expanded', 'false');
  }

  function openNav() {
    sidebar.classList.add('open');
    backdrop.classList.add('open');
    toggle.setAttribute('aria-expanded', 'true');
  }

  toggle.addEventListener('click', function () {
    if (sidebar.classList.contains('open')) closeNav();
    else openNav();
  });

  backdrop.addEventListener('click', closeNav);

  sidebar.querySelectorAll('a.nav-link').forEach(function (link) {
    link.addEventListener('click', closeNav);
  });

  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') closeNav();
  });
})();
