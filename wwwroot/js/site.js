(function () {
  "use strict";

  const body = document.body;
  const sidebar = document.querySelector(".app-sidebar");
  const backdrop = document.getElementById("sidebarBackdrop");

  function closeSidebar() {
    body.classList.remove("sidebar-open");
    if (backdrop) backdrop.classList.add("hidden");
  }
  function openSidebar() {
    body.classList.add("sidebar-open");
    if (backdrop) backdrop.classList.remove("hidden");
  }

  document.querySelectorAll("[data-sidebar-open]").forEach((el) =>
    el.addEventListener("click", openSidebar)
  );
  document.querySelectorAll("[data-sidebar-close]").forEach((el) =>
    el.addEventListener("click", closeSidebar)
  );
  if (sidebar) {
    sidebar.querySelectorAll("a").forEach((a) =>
      a.addEventListener("click", () => {
        if (window.innerWidth < 1024) closeSidebar();
      })
    );
  }

  // Dropdown menus
  function closeAllDropdowns(except) {
    document.querySelectorAll("[data-dropdown]").forEach((menu) => {
      if (menu !== except) menu.classList.add("hidden");
    });
  }
  document.querySelectorAll("[data-dropdown-toggle]").forEach((trigger) => {
    trigger.addEventListener("click", function (e) {
      e.stopPropagation();
      const menu = document.getElementById(trigger.getAttribute("data-dropdown-toggle"));
      if (!menu) return;
      const willOpen = menu.classList.contains("hidden");
      closeAllDropdowns(menu);
      menu.classList.toggle("hidden", !willOpen);
    });
  });
  document.addEventListener("click", (e) => {
    if (!e.target.closest("[data-dropdown]")) closeAllDropdowns();
  });
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape") {
      closeAllDropdowns();
      closeSidebar();
    }
  });

  // Public mobile menu
  const publicToggle = document.querySelector("[data-public-menu-toggle]");
  const publicMenu = document.getElementById("publicMenu");
  if (publicToggle && publicMenu) {
    publicToggle.addEventListener("click", () => publicMenu.classList.toggle("hidden"));
  }

  // Confirmation prompts
  document.querySelectorAll("[data-confirm]").forEach((el) => {
    const message = el.getAttribute("data-confirm") || "Are you sure?";
    if (el.tagName === "FORM") {
      el.addEventListener("submit", (e) => {
        if (!window.confirm(message)) e.preventDefault();
      });
    } else {
      el.addEventListener("click", (e) => {
        if (!window.confirm(message)) e.preventDefault();
      });
    }
  });

  // Print buttons
  document.querySelectorAll("[data-print]").forEach((el) => {
    el.addEventListener("click", () => window.print());
  });

  // Demo account fill
  document.querySelectorAll("[data-fill-email]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const email = document.getElementById("Email");
      const password = document.getElementById("Password");
      if (email) email.value = btn.getAttribute("data-fill-email");
      if (password) password.value = btn.getAttribute("data-fill-password") || "";
      email?.focus();
    });
  });

  // Auto submit filter forms
  document.querySelectorAll("[data-autosubmit]").forEach((el) => {
    el.addEventListener("change", () => el.form?.submit());
  });

  // Password visibility
  document.querySelectorAll("[data-toggle-password]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const input = document.getElementById(btn.getAttribute("data-toggle-password"));
      if (!input) return;
      input.type = input.type === "password" ? "text" : "password";
      btn.classList.toggle("text-brand-700");
    });
  });

  // Attendance quick fill
  document.querySelectorAll("[data-mark-all]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const value = btn.getAttribute("data-mark-all");
      const form = btn.closest("form");
      if (!form) return;
      form.querySelectorAll("select[data-attendance-status]").forEach((select) => {
        select.value = value;
        select.dispatchEvent(new Event("change", { bubbles: true }));
      });
      form.querySelectorAll("input[type=radio][data-attendance-status]").forEach((radio) => {
        if (radio.value === value) radio.checked = true;
      });
    });
  });

  document.querySelectorAll("select[data-attendance-status]").forEach((select) => {
    const apply = () => {
      select.classList.remove("border-emerald-300", "bg-emerald-50", "text-emerald-800", "border-red-300", "bg-red-50", "text-red-700", "border-amber-300", "bg-amber-50", "text-amber-800", "border-slate-300", "bg-slate-50");
      if (select.value === "Present") select.classList.add("border-emerald-300", "bg-emerald-50", "text-emerald-800");
      else if (select.value === "Absent") select.classList.add("border-red-300", "bg-red-50", "text-red-700");
      else if (select.value === "Late") select.classList.add("border-amber-300", "bg-amber-50", "text-amber-800");
      else select.classList.add("border-slate-300", "bg-slate-50");
    };
    select.addEventListener("change", apply);
    apply();
  });

  // Client-side table filtering
  document.querySelectorAll("[data-table-filter]").forEach((input) => {
    const target = document.querySelector(input.getAttribute("data-table-filter"));
    if (!target) return;
    input.addEventListener("input", () => {
      const term = input.value.trim().toLowerCase();
      target.querySelectorAll("tbody tr").forEach((row) => {
        row.classList.toggle("hidden", term.length > 0 && !row.textContent.toLowerCase().includes(term));
      });
    });
  });

  // Native dialog modals
  document.querySelectorAll("[data-modal-open]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const dialog = document.getElementById(btn.getAttribute("data-modal-open"));
      if (dialog && typeof dialog.showModal === "function") dialog.showModal();
    });
  });
  document.querySelectorAll("[data-modal-close]").forEach((btn) => {
    btn.addEventListener("click", () => btn.closest("dialog")?.close());
  });
  document.querySelectorAll("dialog").forEach((dialog) => {
    dialog.addEventListener("click", (e) => {
      const rect = dialog.getBoundingClientRect();
      const inside = e.clientX >= rect.left && e.clientX <= rect.right && e.clientY >= rect.top && e.clientY <= rect.bottom;
      if (!inside) dialog.close();
    });
  });

  // Copy to clipboard
  document.querySelectorAll("[data-copy]").forEach((btn) => {
    btn.addEventListener("click", async () => {
      const text = btn.getAttribute("data-copy") || "";
      try {
        await navigator.clipboard.writeText(text);
        const previous = btn.textContent;
        btn.textContent = document.documentElement.lang === "ar" ? "تم النسخ" : "Copied";
        setTimeout(() => (btn.textContent = previous), 1600);
      } catch (_) {}
    });
  });

  // Auto-close dismissible notices
  document.querySelectorAll("[data-dismiss]").forEach((el) => {
    el.addEventListener("click", () => el.closest(".alert")?.remove());
  });
})();
